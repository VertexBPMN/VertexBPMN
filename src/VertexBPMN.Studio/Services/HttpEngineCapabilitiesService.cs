using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpEngineCapabilitiesService : IEngineCapabilitiesService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpEngineCapabilitiesService> _logger;

    public HttpEngineCapabilitiesService(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpEngineCapabilitiesService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
        _logger = logger;
    }

    public EngineCapabilities? Current { get; private set; }

    public async Task<EngineCapabilities> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var capabilities = await _httpClient.GetFromJsonAsync<EngineCapabilities>(
                "api/engine/capabilities", cancellationToken);
            Current = capabilities ?? throw new InvalidOperationException("The API returned no engine capabilities.");
            return Current;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not load engine capabilities");
            throw;
        }
    }
}
