using System.Xml.Linq;

namespace VertexBPMN.Parsing.Performance;

/// <summary>
/// Lazy wrapper for XElement that delays deep cloning until first access.
/// Reduces memory usage when raw extension elements are captured but not frequently accessed.
/// </summary>
public sealed class LazyXElement
{
    private XElement? _originalElement;
    private XElement? _clonedElement;
    private readonly object _lock = new();
    
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
            if (_clonedElement != null)
                return _clonedElement;
                
            lock (_lock)
            {
                if (_clonedElement != null)
                    return _clonedElement;
                    
                _clonedElement = new XElement(_originalElement!);
                _originalElement = null; // Release reference to original
                return _clonedElement;
            }
        }
    }
    
    /// <summary>
    /// Gets whether the element has been cloned yet (for diagnostics).
    /// </summary>
    public bool IsCloned => _clonedElement != null;
}