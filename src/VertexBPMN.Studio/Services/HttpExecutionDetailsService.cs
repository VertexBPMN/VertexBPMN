using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpExecutionDetailsService(IHttpClientFactory httpClientFactory) : IExecutionDetailsService
{
    public Task<JsonElement> GetJobsAsync(string? tenantId = null, CancellationToken cancellationToken = default) =>
        GetAsync(BuildUri("/api/vertex/job", tenantId), cancellationToken);

    public Task<JsonElement> GetIncidentsAsync(string? tenantId = null, CancellationToken cancellationToken = default) =>
        GetAsync(BuildUri("/api/vertex/incident", tenantId), cancellationToken);

    public Task<JsonElement> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/vertex/variable/{processInstanceId}", cancellationToken);

    private static string BuildUri(string path, string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? path
            : $"{path}?tenantId={Uri.EscapeDataString(tenantId)}";

    private async Task<JsonElement> GetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
