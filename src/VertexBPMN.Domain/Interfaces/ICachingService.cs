namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Production-Grade Caching Service
/// Olympic-level feature: Production-Grade Features - Performance Optimization
/// </summary>
public interface ICachingService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemovePatternAsync(string pattern);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
}