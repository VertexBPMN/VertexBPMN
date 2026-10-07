using System.Xml.Linq;

namespace VertexBPMN.Engine.Performance;

/// <summary>
/// Lazy wrapper for XElement that delays deep cloning until first access.
/// Reduces memory usage when raw extension elements are captured but not frequently accessed.
/// </summary>
public sealed class LazyXElement
{
	private XElement? _originalElement;
	private XElement? _clonedElement;
	private readonly Lock _lock = new();

	public LazyXElement(XElement original)
	{
		_originalElement = original ?? throw new ArgumentNullException(nameof(original));
	}

	/// <summary>
	/// Gets the cloned XElement, performing deep clone on first access.
	/// Thread-safe with double-checked locking pattern.
	/// </summary>
	public XElement Element
	{
		get
		{
			var cloned = Volatile.Read(ref _clonedElement);
			if (cloned != null)
			{
				return cloned;
			}

			lock (_lock)
			{
				cloned = _clonedElement;
				if (cloned != null)
				{
					return cloned;
				}

				cloned = new XElement(_originalElement!);
				Volatile.Write(ref _clonedElement, cloned);
				_originalElement = null; // Release reference to original
				return cloned;
			}
		}
	}

	/// <summary>
	/// Gets whether the element has been cloned yet (for diagnostics).
	/// </summary>
	public bool IsCloned => Volatile.Read(ref _clonedElement) != null;
}
