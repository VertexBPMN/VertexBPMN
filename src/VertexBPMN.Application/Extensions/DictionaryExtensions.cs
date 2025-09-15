namespace VertexBPMN.Application.Extensions;

/// <summary>
/// Extension methods for dictionary operations used by service task handlers.
/// </summary>
internal static class DictionaryExtensions
{
    /// <summary>
    /// Gets the value associated with the specified key or returns the default value if the key is not found.
    /// </summary>
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }
}