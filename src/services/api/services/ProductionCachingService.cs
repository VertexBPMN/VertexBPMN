using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VertexBPMN.Core;
using VertexBPMN.Core.Contracts;

namespace VertexBPMN.Api.Services;

/// <summary>
/// Production-Grade Caching Service
/// Olympic-level feature: Production-Grade Features - Performance Optimization
/// </summary>
public class ProductionCachingService : VertexBPMN.Core.Contracts.ICachingService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ProductionCachingService> _logger;
    private readonly Dictionary<string, string> _keyRegistry = new();
    private readonly object _registryLock = new();

    public ProductionCachingService(IMemoryCache memoryCache, ILogger<ProductionCachingService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var cached))
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return (T?)cached;
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return default(T);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            
            if (expiry.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiry.Value;
            }
            else
            {
                options.SlidingExpiration = TimeSpan.FromMinutes(30); // Default expiry
            }

            options.RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                _logger.LogDebug("Cache entry evicted - Key: {Key}, Reason: {Reason}", key, reason);
                lock (_registryLock)
                {
                    _keyRegistry.Remove(key.ToString()!);
                }
            });

            _memoryCache.Set(key, value, options);
            
            lock (_registryLock)
            {
                _keyRegistry[key] = typeof(T).Name;
            }

            _logger.LogDebug("Cache set for key: {Key}, Type: {Type}, Expiry: {Expiry}", 
                key, typeof(T).Name, expiry?.ToString() ?? "Default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            _memoryCache.Remove(key);
            lock (_registryLock)
            {
                _keyRegistry.Remove(key);
            }
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }
    }

    public async Task RemovePatternAsync(string pattern)
    {
        try
        {
            List<string> keysToRemove = new();
            
            lock (_registryLock)
            {
                keysToRemove = _keyRegistry.Keys
                    .Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var key in keysToRemove)
            {
                await RemoveAsync(key);
            }

            _logger.LogDebug("Cache pattern removal completed - Pattern: {Pattern}, Removed: {Count}", 
                pattern, keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache pattern: {Pattern}", pattern);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        var cached = await GetAsync<T>(key);
        if (cached != null)
        {
            return cached;
        }

        try
        {
            var value = await factory();
            await SetAsync(key, value, expiry);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrSet factory for key: {Key}", key);
            throw;
        }
    }
}