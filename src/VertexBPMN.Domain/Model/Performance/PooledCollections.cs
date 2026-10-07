using System.Buffers;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace VertexBPMN.Engine.Performance;

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
