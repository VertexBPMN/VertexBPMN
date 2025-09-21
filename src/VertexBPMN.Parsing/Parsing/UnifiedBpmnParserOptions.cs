namespace VertexBPMN.Parsing;

/// <summary>
/// Options controlling parsing performance and validation behavior.
/// </summary>
public sealed class UnifiedBpmnParserOptions
{
    /// <summary>Enable strict validation diagnostics (default true).</summary>
    public bool StrictValidation { get; init; } = true;
    /// <summary>Preserve unknown vendor extension elements/attributes (default true).</summary>
    public bool PreserveUnknownExtensions { get; init; } = true;
    /// <summary>Maximum number of cached parsed models (0 disables caching).</summary>
    public int CacheSize { get; init; } = 0;
    /// <summary>Parse BPMN Diagram Interchange (currently not implemented) placeholder.</summary>
    public bool ParseDiagramInterchange { get; init; } = false;
}
