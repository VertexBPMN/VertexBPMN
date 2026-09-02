using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Studio.Services;

public sealed class HttpDmnService : IDmnService
{
    private readonly HttpClient _httpClient;

    public HttpDmnService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
    }

    public async Task DeployAsync(
        string decisionKey,
        string name,
        string dmnXml,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/decision/deploy",
            new DeployRequest(decisionKey, name, dmnXml, tenantId),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<DecisionDefinition?> GetByKeyAsync(
        string decisionKey,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/decision/by-key?decisionKey={Uri.EscapeDataString(decisionKey)}";
        if (!string.IsNullOrWhiteSpace(tenantId))
            query += $"&tenantId={Uri.EscapeDataString(tenantId)}";
        return _httpClient.GetFromJsonAsync<DecisionDefinition>(query, cancellationToken);
    }

    public async Task<DecisionResult> EvaluateAsync(
        string decisionKey,
        IDictionary<string, object> inputs,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/decision/evaluate",
            new EvaluateRequest(decisionKey, inputs, tenantId),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DecisionResult>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The API returned no decision result.");
    }

    public Task<JsonElement> ListDefinitionsAsync(string? key = null, string? tenantId = null, CancellationToken cancellationToken = default) =>
        GetListAsync("api/vertex/decision-definition", new Dictionary<string, string?>
        {
            ["key"] = key,
            ["tenantId"] = tenantId
        }, cancellationToken);

    public Task<JsonElement> ListInstancesAsync(string? decisionKey = null, string? tenantId = null, CancellationToken cancellationToken = default) =>
        GetListAsync("api/vertex/decision-instance", new Dictionary<string, string?>
        {
            ["decisionKey"] = decisionKey,
            ["tenantId"] = tenantId
        }, cancellationToken);

    private async Task<JsonElement> GetListAsync(string path, Dictionary<string, string?> parameters, CancellationToken cancellationToken)
    {
        var query = string.Join("&", parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value!)}"));
        var uri = string.IsNullOrWhiteSpace(query) ? path : $"{path}?{query}";
        return await _httpClient.GetFromJsonAsync<JsonElement>(uri, cancellationToken);
    }

    private sealed record DeployRequest(string DecisionKey, string Name, string DmnXml, string? TenantId);
    private sealed record EvaluateRequest(string DecisionKey, IDictionary<string, object> Inputs, string? TenantId);
}
