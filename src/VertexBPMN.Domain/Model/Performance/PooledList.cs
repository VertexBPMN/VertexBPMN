using System.Buffers;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace VertexBPMN.Engine.Performance;

/// <summary>
/// A List-like collection that uses ArrayPool for its backing storage.
/// Must be disposed to return arrays to the pool.
/// </summary>
public sealed class PooledList<T> : IDisposable
{
	private static readonly ArrayPool<T> Pool = ArrayPool<T>.Shared;

	private T[] _array;
	private bool _disposed;

	public PooledList(int initialCapacity = 4)
	{
		_array = Pool.Rent(Math.Max(initialCapacity, 4));
		Count = 0;
	}

	public int Count { get; private set; }

	public T this[int index]
	{
		get
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

			return _array[index];
		}
		set
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

			_array[index] = value;
		}
	}

	public void Add(T item)
	{
		EnsureCapacity(Count + 1);
		_array[Count++] = item;
	}

	public void Clear()
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			Array.Clear(_array, 0, Count);
		}
		Count = 0;
	}

	private void EnsureCapacity(int capacity)
	{
		if (capacity <= _array.Length)
		{
			return;
		}

		var newSize = Math.Max(capacity, _array.Length * 2);
		var newArray = Pool.Rent(newSize);
		Array.Copy(_array, newArray, Count);
		Pool.Return(_array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
		_array = newArray;
	}

	public List<T> ToList()
	{
		// Convert to regular list
		var regularList = new List<T>(Count);
		for (var i = 0; i < Count; i++)
		{
			regularList.Add(_array[i]);
		}
		return regularList;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		Pool.Return(_array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
		_array = null!;
		_disposed = true;
	}
}
