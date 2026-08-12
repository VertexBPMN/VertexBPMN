namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Security configuration options for BPMN parsing.
/// </summary>
public sealed record BpmnSecurityOptions
{
    /// <summary>
    /// Maximum XML file size in bytes (default: 100MB).
    /// </summary>
    public long MaxXmlSizeBytes { get; init; } = 100 * 1024 * 1024;
    
    /// <summary>
    /// Maximum XML nesting depth (default: 100).
    /// </summary>
    public int MaxXmlDepth { get; init; } = 100;
    
    /// <summary>
    /// Maximum estimated element count (default: 100,000).
    /// </summary>
    public int MaxElementCount { get; init; } = 100_000;
    
    /// <summary>
    /// Maximum memory usage during parsing (default: 500MB).
    /// </summary>
    public long MaxMemoryUsageBytes { get; init; } = 500 * 1024 * 1024;
    
    /// <summary>
    /// Maximum parse operation timeout (default: 30 seconds).
    /// </summary>
    public TimeSpan ParseTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Enable detailed security logging.
    /// </summary>
    public bool EnableSecurityLogging { get; init; } = true;
}