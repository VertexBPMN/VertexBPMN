using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;
internal sealed class HttpEngineCapabilitiesService : IEngineCapabilitiesService
{
    private readonly HttpClient _httpClient;
    public HttpEngineCapabilitiesService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
    }

    public EngineCapabilities? Current { get; private set; }

    public async Task<EngineCapabilities> GetAsync(CancellationToken cancellationToken = default)
    {
        var capabilities = await _httpClient.GetFromJsonAsync<EngineCapabilities>("api/engine/capabilities", cancellationToken);
        Current = capabilities ?? throw new InvalidOperationException("The API returned no engine capabilities.");
        return Current;
    }
}
