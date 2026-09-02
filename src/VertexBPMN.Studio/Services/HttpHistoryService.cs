using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public class HttpHistoryService : IHistoryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpHistoryService> _logger;

    public HttpHistoryService(IHttpClientFactory httpClientFactory, ILogger<HttpHistoryService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
        _logger = logger;
    }

    public async Task<IEnumerable<HistoryEvent>> GetHistoryAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = string.IsNullOrWhiteSpace(tenantId)
                ? string.Empty
                : $"?tenantId={Uri.EscapeDataString(tenantId)}";
            var events = await _httpClient.GetFromJsonAsync<IEnumerable<HistoryEvent>>(
                $"api/history{query}", cancellationToken);
            return events ?? Enumerable.Empty<HistoryEvent>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching history for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<IEnumerable<HistoryEvent>> GetHistoryByProcessInstanceAsync(Guid processInstanceId)
    {
        try
        {
            var events = await _httpClient.GetFromJsonAsync<IEnumerable<HistoryEvent>>($"api/history/by-process-instance/{processInstanceId}");
            return events ?? Enumerable.Empty<HistoryEvent>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching history for process instance {ProcessInstanceId}", processInstanceId);
            return Enumerable.Empty<HistoryEvent>();
        }
    }

    public async Task<HistoryEvent?> GetHistoryEventByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<HistoryEvent>($"api/history/{id}");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching history event {Id}", id);
            return null;
        }
    }
}