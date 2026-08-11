using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpMlAnalyticsService(IHttpClientFactory httpClientFactory) : IMlAnalyticsService
{
    public Task<JsonElement> PredictCompletionAsync(Guid processInstanceId, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/ml/predict/completion/{processInstanceId}", cancellationToken);

    public async Task<JsonElement> PredictDurationAsync(
        string processDefinitionKey,
        IDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(
            "/api/ml/predict/duration",
            new { processDefinitionKey, variables = variables ?? new Dictionary<string, object?>() },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public Task<JsonElement> PredictBottlenecksAsync(string processDefinitionKey, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/ml/predict/bottlenecks/{Uri.EscapeDataString(processDefinitionKey)}", cancellationToken);

    public Task<JsonElement> GetOptimizationSuggestionsAsync(string processDefinitionKey, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/ml/optimize/{Uri.EscapeDataString(processDefinitionKey)}", cancellationToken);

    private async Task<JsonElement> GetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        return await client.GetFromJsonAsync<JsonElement>(uri, cancellationToken);
    }
}
