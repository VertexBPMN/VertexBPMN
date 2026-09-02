using System.Net.Http.Json;
namespace VertexBPMN.Studio.Services;
public sealed class HttpFormDefinitionService(IHttpClientFactory factory) : IFormDefinitionService
{
    private HttpClient Client => factory.CreateClient("VertexBPMN.Api");
    public async Task<IReadOnlyList<StudioFormDefinition>> ListAsync(string tenantId, CancellationToken ct = default) => await Client.GetFromJsonAsync<List<StudioFormDefinition>>($"api/forms?tenantId={Uri.EscapeDataString(tenantId)}", ct) ?? [];
    public Task<StudioFormDefinition?> GetAsync(string id, string tenantId, CancellationToken ct = default) => Client.GetFromJsonAsync<StudioFormDefinition>($"api/forms/{Uri.EscapeDataString(id)}?tenantId={Uri.EscapeDataString(tenantId)}", ct);
    public async Task<StudioFormDefinition> CreateAsync(StudioFormWriteRequest request, CancellationToken ct = default) { using var r = await Client.PostAsJsonAsync("api/forms", request, ct); r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<StudioFormDefinition>(cancellationToken: ct) ?? throw new InvalidOperationException("The API returned no form."); }
    public async Task UpdateAsync(string id, StudioFormWriteRequest request, CancellationToken ct = default) { using var r = await Client.PutAsJsonAsync($"api/forms/{Uri.EscapeDataString(id)}", request, ct); r.EnsureSuccessStatusCode(); }
    public async Task DeleteAsync(string id, string tenantId, CancellationToken ct = default) { using var r = await Client.DeleteAsync($"api/forms/{Uri.EscapeDataString(id)}?tenantId={Uri.EscapeDataString(tenantId)}", ct); r.EnsureSuccessStatusCode(); }
}
