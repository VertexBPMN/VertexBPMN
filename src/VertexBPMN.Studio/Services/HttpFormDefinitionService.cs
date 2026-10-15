using System.Net.Http.Json;
namespace VertexBPMN.Studio.Services;
internal sealed class HttpFormDefinitionService(IHttpClientFactory factory) : IFormDefinitionService
{
    private HttpClient Client => factory.CreateClient("VertexBPMN.Api");
    public async Task<IReadOnlyList<StudioFormDefinition>> ListAsync(string tenantId, CancellationToken cancellationToken = default) => await Client.GetFromJsonAsync<List<StudioFormDefinition>>($"api/forms?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken) ?? [];
    public Task<StudioFormDefinition?> GetAsync(string id, string tenantId, CancellationToken cancellationToken = default) => Client.GetFromJsonAsync<StudioFormDefinition>($"api/forms/{Uri.EscapeDataString(id)}?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken);
    public async Task<IReadOnlyList<StudioFormRevision>> GetRevisionsAsync(string id, string tenantId, CancellationToken cancellationToken = default) => await Client.GetFromJsonAsync<List<StudioFormRevision>>($"api/forms/{Uri.EscapeDataString(id)}/revisions?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken) ?? [];
    public Task<StudioFormRevision?> GetRevisionAsync(string id, int version, string tenantId, CancellationToken cancellationToken = default) => Client.GetFromJsonAsync<StudioFormRevision>($"api/forms/{Uri.EscapeDataString(id)}/revisions/{version}?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken);
    public async Task<StudioFormDefinition> CreateAsync(StudioFormWriteRequest request, CancellationToken cancellationToken = default) { using var r = await Client.PostAsJsonAsync("api/forms", request, cancellationToken); r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<StudioFormDefinition>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("The API returned no form."); }
    public async Task UpdateAsync(string id, StudioFormWriteRequest request, CancellationToken cancellationToken = default) { using var r = await Client.PutAsJsonAsync($"api/forms/{Uri.EscapeDataString(id)}", request, cancellationToken); r.EnsureSuccessStatusCode(); }
    public async Task DeleteAsync(string id, string tenantId, CancellationToken cancellationToken = default) { using var r = await Client.DeleteAsync($"api/forms/{Uri.EscapeDataString(id)}?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken); r.EnsureSuccessStatusCode(); }
}
