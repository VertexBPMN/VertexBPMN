using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpExternalTaskOperationsService(IHttpClientFactory httpClientFactory) : IExternalTaskOperationsService
{
    public Task<IReadOnlyList<StudioExternalTaskProfile>> ListProfilesAsync(
        string tenantId, CancellationToken cancellationToken = default) =>
        GetAsync<StudioExternalTaskProfile>($"/api/external-task-operations/catalog?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken);

    public Task<IReadOnlyList<StudioExternalTaskOperation>> ListForProcessAsync(
        string tenantId, Guid processInstanceId, string? activityId = null, CancellationToken cancellationToken = default)
    {
        var uri = $"/api/external-task-operations/process/{processInstanceId}?tenantId={Uri.EscapeDataString(tenantId)}";
        if (!string.IsNullOrWhiteSpace(activityId)) uri += $"&activityId={Uri.EscapeDataString(activityId)}";
        return GetAsync<StudioExternalTaskOperation>(uri, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T[]>(cancellationToken) ?? [];
    }
}
