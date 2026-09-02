using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpMigrationService(IHttpClientFactory httpClientFactory) : IMigrationService
{
    public async Task<JsonElement> PreviewAsync(
        string sourceProcessDefinitionId,
        string targetProcessDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(
            "/api/process-migration/plan/preview",
            new { sourceProcessDefinitionId, targetProcessDefinitionId },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public async Task<JsonElement> ExecuteAsync(JsonElement plan, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync("/api/process-migration/plan/execute", plan, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public Task<JsonElement> GetStatusAsync(string migrationId, CancellationToken cancellationToken = default) =>
        SendAsync($"/api/migration/status/{Uri.EscapeDataString(migrationId)}", HttpMethod.Get, cancellationToken);

    public Task<JsonElement> CreateSnapshotAsync(string processInstanceId, CancellationToken cancellationToken = default) =>
        SendAsync($"/api/migration/snapshot/{Uri.EscapeDataString(processInstanceId)}", HttpMethod.Post, cancellationToken);

    public Task<JsonElement> RestoreFromSnapshotAsync(
        string processInstanceId,
        string snapshotId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            $"/api/migration/restore/{Uri.EscapeDataString(processInstanceId)}/{Uri.EscapeDataString(snapshotId)}",
            HttpMethod.Post,
            cancellationToken);

    public Task<JsonElement> RollbackAsync(string migrationId, CancellationToken cancellationToken = default) =>
        SendAsync($"/api/migration/rollback/{Uri.EscapeDataString(migrationId)}", HttpMethod.Post, cancellationToken);

    private async Task<JsonElement> SendAsync(string requestUri, HttpMethod method, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var request = new HttpRequestMessage(method, requestUri);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
