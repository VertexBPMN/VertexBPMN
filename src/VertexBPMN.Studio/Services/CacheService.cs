using Microsoft.JSInterop;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public class CacheService : ICacheService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<CacheService> _logger;
    private readonly Dictionary<string, (object Value, DateTime Expiration)> _memoryCache = new();

    public CacheService(IJSRuntime jsRuntime, ILogger<CacheService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            // Check memory cache first
            if (_memoryCache.TryGetValue(key, out var cached) && cached.Expiration > DateTime.Now)
            {
                return cached.Value as T;
            }

            // Try localStorage/IndexedDB
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            if (!string.IsNullOrEmpty(json))
            {
                var result = JsonSerializer.Deserialize<T>(json);
                if (result != null)
                {
                    _memoryCache[key] = (result, DateTime.Now.AddMinutes(15));
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cached value for key: {Key}", key);
        }

        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var exp = expiration ?? TimeSpan.FromMinutes(15);
            _memoryCache[key] = (value, DateTime.Now.Add(exp));

            var json = JsonSerializer.Serialize(value);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache value for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            _memoryCache.Remove(key);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cached value for key: {Key}", key);
        }
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("navigator.onLine");
        }
        catch
        {
            return true; // Assume online if check fails
        }
    }
}