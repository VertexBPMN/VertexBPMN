using System.Collections.Generic;
using System.Xml.Linq;

namespace VertexBPMN.Parsing.Ecosystem;

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
/// Result of vendor extension processing.
/// </summary>
public sealed record VendorExtensionResult
{
    /// <summary>
    /// Normalized attributes extracted from the vendor extension.
    /// </summary>
    public Dictionary<string, string> NormalizedAttributes { get; init; } = new();
    
    /// <summary>
    /// Validation diagnostics generated during processing.
    /// </summary>
    public List<string> Diagnostics { get; init; } = new();
    
    /// <summary>
    /// Whether the extension should be preserved in raw form.
    /// </summary>
    public bool PreserveRawElement { get; init; } = true;
}