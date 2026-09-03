using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpSimulationService(IHttpClientFactory httpClientFactory) : ISimulationService
{
    public async Task<JsonElement> SimulateAsync(
        string bpmnXml,
        string processDefinitionId,
        IDictionary<string, object?>? variables = null,
        int? maxSteps = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(
            "/api/simulation",
            new
            {
                bpmnXml,
                processDefinitionId,
                // The API treats Variables as a required, non-nullable member; never send null
                // or the request fails model validation with 400 "The Variables field is required."
                variables = variables ?? (object)new Dictionary<string, object>(),
                maxSteps,
                tenantId
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public Task<JsonElement> GetSummaryAsync(JsonElement simulationResult, CancellationToken cancellationToken = default) =>
        PostAnalyticsAsync("/api/simulation-analytics/summary", simulationResult, cancellationToken);

    public Task<JsonElement> GetStepBreakdownAsync(JsonElement simulationResult, CancellationToken cancellationToken = default) =>
        PostAnalyticsAsync("/api/simulation-analytics/steps", simulationResult, cancellationToken);

    public Task<JsonElement> GetVariableTraceAsync(JsonElement simulationResult, string variableName, CancellationToken cancellationToken = default) =>
        PostAnalyticsAsync($"/api/simulation-analytics/variable-trace/{Uri.EscapeDataString(variableName)}", simulationResult, cancellationToken);

    public Task<JsonElement> CompareAsync(JsonElement resultA, JsonElement resultB, CancellationToken cancellationToken = default) =>
        PostAnalyticsAsync("/api/simulation-analytics/compare", new { resultA, resultB }, cancellationToken);

    private async Task<JsonElement> PostAnalyticsAsync(
        string uri,
        object simulationResult,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(uri, simulationResult, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
