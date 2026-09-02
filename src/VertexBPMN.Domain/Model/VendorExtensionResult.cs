namespace VertexBPMN.Domain.Model;

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