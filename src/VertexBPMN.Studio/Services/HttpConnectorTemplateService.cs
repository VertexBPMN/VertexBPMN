using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpConnectorTemplateService(IHttpClientFactory httpClientFactory) : IConnectorTemplateService
{
    public async Task<IReadOnlyList<StudioConnectorTemplate>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        return await client.GetFromJsonAsync<List<StudioConnectorTemplate>>(
            $"/api/connector-templates?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken) ?? [];
    }
}
