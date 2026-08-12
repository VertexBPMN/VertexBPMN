using System.Xml.Linq;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Phase 12: Pluggable vendor extension interpreter interface.
/// Allows custom handling of vendor-specific BPMN extensions.
/// </summary>
public interface IBpmnVendorExtensionInterpreter
{
    /// <summary>
    /// Namespaces this handler can process.
    /// </summary>
    string[] SupportedNamespaces { get; }
    
    /// <summary>
    /// Determines if this handler can process the given extension element.
    /// </summary>
    /// <param name="namespaceUri">The namespace URI of the element</param>
    /// <param name="localName">The local name of the element</param>
    /// <returns>True if this handler can process the element</returns>
    bool CanHandle(string namespaceUri, string localName);
    
    /// <summary>
    /// Processes a vendor extension element and returns normalized attributes.
    /// </summary>
    /// <param name="element">The XML element to process</param>
    /// <param name="ownerElementId">The ID of the BPMN element that contains this extension</param>
    /// <returns>Processing result with normalized attributes</returns>
    VendorExtensionResult ProcessExtension(XElement element, string ownerElementId);
}

/// <summary>
/// Result of processing a vendor extension element.
/// </summary>
public record VendorExtensionResult(
    bool Success,
    IReadOnlyDictionary<string, string>? NormalizedAttributes = null,
    string? ErrorMessage = null
);
