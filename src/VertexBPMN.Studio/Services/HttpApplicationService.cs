using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

internal sealed class HttpApplicationService(IHttpClientFactory factory) : IApplicationService
{
    private HttpClient Client => factory.CreateClient("VertexBPMN.Api");

    public async Task<IReadOnlyList<StudioApplicationSummary>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
        => await Client.GetFromJsonAsync<List<StudioApplicationSummary>>($"api/applications?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken) ?? [];

    public async Task<StudioApplicationDraft?> GetDraftAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync($"api/applications/{Uri.EscapeDataString(id)}?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await ApiResponseErrors.EnsureSuccessAsync(response, cancellationToken);
        var metadata = await response.Content.ReadFromJsonAsync<StudioApplicationDraft>(cancellationToken: cancellationToken);
        return metadata;
    }

    public async Task<StudioApplicationSummary> CreateAsync(string tenantId, string key, string name, string content, CancellationToken cancellationToken = default)
    {
        using var response = await Client.PostAsJsonAsync("api/applications", new
        {
            tenantId,
            key,
            name,
            expectedVersion = 0,
            content
        }, cancellationToken);
        await ApiResponseErrors.EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<StudioApplicationSummary>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The API returned no application.");
    }

    public async Task SaveDraftAsync(string id, string tenantId, string key, string name, int expectedVersion, string content, CancellationToken cancellationToken = default)
    {
        using var response = await Client.PutAsJsonAsync($"api/applications/{Uri.EscapeDataString(id)}", new
        {
            tenantId,
            key,
            name,
            expectedVersion,
            content
        }, cancellationToken);
        await ApiResponseErrors.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task PublishAsync(string id, string tenantId, int version, CancellationToken cancellationToken = default)
    {
        // The designer's 'Veröffentlichen' turns the current revision into a released
        // revision through the real release pipeline (release-candidate then release).
        using var candidateResponse = await Client.PostAsync(
            $"api/applications/{Uri.EscapeDataString(id)}/revisions/{version}/release-candidate?tenantId={Uri.EscapeDataString(tenantId)}",
            null, cancellationToken);
        await ApiResponseErrors.EnsureSuccessAsync(candidateResponse, cancellationToken);
        var candidateId = (await candidateResponse.Content.ReadFromJsonAsync<PublishCandidate>(cancellationToken: cancellationToken))?.Id
            ?? throw new InvalidOperationException("The API returned no release candidate.");
        using var releaseResponse = await Client.PostAsync(
            $"api/applications/release-candidates/{Uri.EscapeDataString(candidateId)}/release?tenantId={Uri.EscapeDataString(tenantId)}",
            null, cancellationToken);
        await ApiResponseErrors.EnsureSuccessAsync(releaseResponse, cancellationToken);
    }

    private sealed record PublishCandidate(string Id);
}
