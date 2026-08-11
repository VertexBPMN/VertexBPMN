using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpExecutionDetailsService(IHttpClientFactory httpClientFactory) : IExecutionDetailsService
{
    public Task<JsonElement> GetJobsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/api/vertex/job", cancellationToken);

    public Task<JsonElement> GetIncidentsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/api/vertex/incident", cancellationToken);

    public Task<JsonElement> GetVariablesAsync(Guid processInstanceId, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/vertex/variable?processInstanceId={processInstanceId}", cancellationToken);

    private async Task<JsonElement> GetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
