using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpCaseManagementService(
    IHttpClientFactory httpClientFactory,
    StudioTenantContext tenantContext) : ICaseManagementService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
    private readonly Dictionary<string, Guid> _activeInstances = new(StringComparer.Ordinal);

    public async Task RegisterModelAsync(
        string caseId,
        string cmmnXml,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/case-definitions/deploy",
            new DeployRequest(caseId, caseId, cmmnXml, tenantContext.CurrentTenantId),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ExecuteCaseAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/case-definitions/{Uri.EscapeDataString(caseId)}/start",
            new TenantRequest(tenantContext.CurrentTenantId),
            cancellationToken);
        var result = await ReadRunResultAsync(response, cancellationToken);
        _activeInstances[caseId] = result.CaseInstanceId;
        return result.Trace;
    }

    public async Task TriggerUserEventAsync(
        string caseId,
        string eventId,
        IReadOnlyDictionary<string, string> eventData,
        CancellationToken cancellationToken = default)
    {
        var instanceId = ResolveInstanceId(caseId);
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/case-definitions/instances/{instanceId:D}/events/{Uri.EscapeDataString(eventId)}",
            new TriggerEventRequest(tenantContext.CurrentTenantId, eventData),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateCaseFileItemAsync(
        string caseId,
        string itemId,
        string value,
        CancellationToken cancellationToken = default)
    {
        var instanceId = ResolveInstanceId(caseId);
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/case-definitions/instances/{instanceId:D}/case-file/{Uri.EscapeDataString(itemId)}",
            new UpdateCaseFileRequest(tenantContext.CurrentTenantId, value),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task GenerateAdHocSubprocessAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        var instanceId = ResolveInstanceId(caseId);
        var query = TenantQuery();
        var instance = await _httpClient.GetFromJsonAsync<CaseInstanceResponse>(
            $"api/case-definitions/instances/{instanceId:D}{query}",
            cancellationToken)
            ?? throw new InvalidOperationException("The API returned no CMMN case instance.");
        var states = JsonSerializer.Deserialize<Dictionary<string, string>>(instance.PlanItemStatesJson) ?? [];
        var planItemId = states.FirstOrDefault(item => item.Value == "Discretionary").Key
            ?? throw new InvalidOperationException("The active case has no available discretionary item.");

        using var response = await _httpClient.PostAsJsonAsync(
            $"api/case-definitions/instances/{instanceId:D}/discretionary-items/{Uri.EscapeDataString(planItemId)}/activate",
            new TenantRequest(tenantContext.CurrentTenantId),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<HistoricalCaseSnapshot>> GetHistoricalContextAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        var instanceId = ResolveInstanceId(caseId);
        var history = await _httpClient.GetFromJsonAsync<HistoryEntry[]>(
            $"api/case-definitions/instances/{instanceId:D}/history{TenantQuery()}",
            cancellationToken) ?? [];
        return history.Select(entry => new HistoricalCaseSnapshot(
            entry.CaseInstanceId.ToString(),
            entry.CaseFile.ToDictionary(
                item => item.Key,
                item => item.Value.ValueKind == JsonValueKind.String
                    ? item.Value.GetString() ?? string.Empty
                    : item.Value.ToString()),
            entry.CompletedPlanItems,
            entry.Timestamp)).ToArray();
    }

    private Guid ResolveInstanceId(string caseId) =>
        Guid.TryParse(caseId, out var instanceId)
            ? instanceId
            : _activeInstances.GetValueOrDefault(caseId) is var active && active != Guid.Empty
                ? active
                : throw new InvalidOperationException($"Execute case '{caseId}' before using runtime actions.");

    private string TenantQuery() => string.IsNullOrWhiteSpace(tenantContext.CurrentTenantId)
        ? string.Empty
        : $"?tenantId={Uri.EscapeDataString(tenantContext.CurrentTenantId)}";

    private static async Task<CaseRunResponse> ReadRunResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CaseRunResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The API returned no CMMN execution result.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"CMMN API returned {(int)response.StatusCode} ({response.ReasonPhrase}): {body}",
            null,
            response.StatusCode);
    }

    private sealed record DeployRequest(string Key, string Name, string CmmnXml, string? TenantId);
    private sealed record TenantRequest(string? TenantId);
    private sealed record TriggerEventRequest(string? TenantId, IReadOnlyDictionary<string, string> EventData);
    private sealed record UpdateCaseFileRequest(string? TenantId, object? Value);
    private sealed record CaseRunResponse(Guid CaseInstanceId, string CaseDefinitionId, string Key, string State, IReadOnlyList<string> Trace);
    private sealed record CaseInstanceResponse(string PlanItemStatesJson);
    private sealed record HistoryEntry(
        Guid CaseInstanceId,
        IReadOnlyDictionary<string, JsonElement> CaseFile,
        IReadOnlyList<string> CompletedPlanItems,
        DateTime Timestamp);
}
