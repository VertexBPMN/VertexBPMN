using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace VertexBPMN.Parsing.Performance;

/// <summary>
/// Utility for pooled temporary collections to reduce GC pressure during parsing.
/// Uses ArrayPool for better memory allocation patterns.
/// </summary>
public static class PooledCollections
{
    private static readonly ArrayPool<string> StringArrayPool = ArrayPool<string>.Shared;
    private static readonly ArrayPool<XElement> XElementArrayPool = ArrayPool<XElement>.Create();
    
    /// <summary>
    /// Rents a string array from the pool. Must be returned via ReturnStringArray.
    /// </summary>
    public static string[] RentStringArray(int minimumLength)
    {
        return StringArrayPool.Rent(minimumLength);
    }
    
    /// <summary>
    /// Returns a string array to the pool. Array contents may be cleared.
    /// </summary>
    public static void ReturnStringArray(string[] array, bool clearArray = true)
    {
        StringArrayPool.Return(array, clearArray);
    }
    
    /// <summary>
    /// Rents an XElement array from the pool. Must be returned via ReturnXElementArray.
    /// </summary>
    public static XElement[] RentXElementArray(int minimumLength)
    {
        return XElementArrayPool.Rent(minimumLength);
    }
    
    /// <summary>
    /// Returns an XElement array to the pool. Array contents may be cleared.
    /// </summary>
    public static void ReturnXElementArray(XElement[] array, bool clearArray = true)
    {
        XElementArrayPool.Return(array, clearArray);
    }
    
    /// <summary>
    /// Creates a pooled list that uses ArrayPool for its backing storage.
    /// Automatically returns arrays to pool when disposed.
    /// </summary>
    public static PooledList<T> CreatePooledList<T>(int initialCapacity = 4)
    {
        return new PooledList<T>(initialCapacity);
    }
}

/// <summary>
/// A List-like collection that uses ArrayPool for its backing storage.
/// Must be disposed to return arrays to the pool.
/// </summary>
public sealed class PooledList<T> : IDisposable
{
    private static readonly ArrayPool<T> Pool = ArrayPool<T>.Shared;
    
    private T[] _array;
    private int _count;
    private bool _disposed;
    
    public PooledList(int initialCapacity = 4)
    {
        _array = Pool.Rent(Math.Max(initialCapacity, 4));
        _count = 0;
    }
    
    public int Count => _count;
    
    public T this[int index]
    {
        get
        {
            if (index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
            return _array[index];
        }
        set
        {
            if (index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
            _array[index] = value;
        }
    }
    
    public void Add(T item)
    {
        EnsureCapacity(_count + 1);
        _array[_count++] = item;
    }
    
    public void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_array, 0, _count);
        }
        _count = 0;
    }
    
    private void EnsureCapacity(int capacity)
    {
        if (capacity <= _array.Length) return;
        
        var newSize = Math.Max(capacity, _array.Length * 2);
        var newArray = Pool.Rent(newSize);
        Array.Copy(_array, newArray, _count);
        Pool.Return(_array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _array = newArray;
    }
    
    public List<T> ToList()
    {
        // Convert to regular list
        var regularList = new List<T>(_count);
        for (int i = 0; i < _count; i++)
        {
            regularList.Add(_array[i]);
        }
        return regularList;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        Pool.Return(_array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _array = null!;
        _disposed = true;
    }
}