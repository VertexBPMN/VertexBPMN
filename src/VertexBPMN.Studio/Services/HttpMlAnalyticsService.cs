using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpMlAnalyticsService(IHttpClientFactory httpClientFactory) : IMlAnalyticsService
{
    public Task<JsonElement> PredictCompletionAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default) =>
        GetAsync(WithTenant($"/api/ml/predict/completion/{processInstanceId}", tenantId), cancellationToken);

    public async Task<JsonElement> PredictDurationAsync(
        string processDefinitionKey,
        IDictionary<string, object?>? variables = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(
            "/api/ml/predict/duration",
            new { processDefinitionKey, variables = variables ?? new Dictionary<string, object?>(), tenantId },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public Task<JsonElement> PredictBottlenecksAsync(string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default) =>
        GetAsync(WithTenant($"/api/ml/predict/bottlenecks/{Uri.EscapeDataString(processDefinitionKey)}", tenantId), cancellationToken);

    public Task<JsonElement> GetOptimizationSuggestionsAsync(string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default) =>
        GetAsync(WithTenant($"/api/ml/optimize/{Uri.EscapeDataString(processDefinitionKey)}", tenantId), cancellationToken);

    public async Task TrainModelsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsync(WithTenant("/api/ml/train", tenantId), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> ExportTrainingDataAsync(
        string? processDefinitionKey = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        var endpoint = "/api/ml/export/training-data";
        if (!string.IsNullOrWhiteSpace(processDefinitionKey))
            endpoint += $"?processDefinitionKey={Uri.EscapeDataString(processDefinitionKey)}";
        endpoint = WithTenant(endpoint, tenantId);
        using var response = await client.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string WithTenant(string endpoint, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return endpoint;
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{endpoint}{separator}tenantId={Uri.EscapeDataString(tenantId)}";
    }

    private async Task<JsonElement> GetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        return await client.GetFromJsonAsync<JsonElement>(uri, cancellationToken);
    }
}
