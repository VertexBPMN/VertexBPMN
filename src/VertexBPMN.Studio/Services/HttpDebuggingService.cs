using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpDebuggingService(IHttpClientFactory httpClientFactory) : IDebuggingService
{
    public async Task<JsonElement> TraceAsync(
        string bpmnXml,
        IDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(
            "/api/debug/trace",
            new { bpmnXml, variables },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public Task<JsonElement> StartSessionAsync(Guid processInstanceId, object? options = null, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/visual-debug/session/start/{processInstanceId}", options, cancellationToken);

    public Task<JsonElement> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/visual-debug/session/{sessionId}", cancellationToken);

    public Task<JsonElement> StopSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/visual-debug/session/stop/{sessionId}", null, cancellationToken);

    public Task<JsonElement> SetBreakpointAsync(Guid sessionId, string activityId, object? condition = null, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/visual-debug/breakpoint/{sessionId}/{Uri.EscapeDataString(activityId)}", condition, cancellationToken);

    public Task<JsonElement> RemoveBreakpointAsync(Guid sessionId, string activityId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"/api/visual-debug/breakpoint/{sessionId}/{Uri.EscapeDataString(activityId)}", cancellationToken);

    public Task<JsonElement> StepOverAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/visual-debug/step/over/{sessionId}", null, cancellationToken);

    public Task<JsonElement> ContinueAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        PostAsync($"/api/visual-debug/continue/{sessionId}", null, cancellationToken);

    public Task<JsonElement> GetProcessVisualizationAsync(Guid processInstanceId, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/visual-debug/visualize/{processInstanceId}", cancellationToken);

    public Task<JsonElement> GetExecutionTraceAsync(Guid processInstanceId, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/visual-debug/trace/{processInstanceId}", cancellationToken);

    public Task<JsonElement> InspectVariablesAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        GetAsync($"/api/visual-debug/variables/{sessionId}", cancellationToken);

    private async Task<JsonElement> GetAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private async Task<JsonElement> PostAsync(string uri, object? body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(uri, body ?? new { }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private async Task<JsonElement> DeleteAsync(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.DeleteAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentLength == 0
            ? JsonDocument.Parse("{}").RootElement
            : await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
