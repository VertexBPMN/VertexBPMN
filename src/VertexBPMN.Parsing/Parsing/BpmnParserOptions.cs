namespace VertexBPMN.Parsing;

/// <summary>
/// Options controlling parsing performance and validation behavior.
/// </summary>
public sealed class BpmnParserOptions
{
    /// <summary>Enable strict validation diagnostics (default true).</summary>
    public bool StrictValidation { get; init; } = true;
    /// <summary>Preserve unknown vendor extension elements/attributes (default true).</summary>
    public bool PreserveUnknownExtensions { get; init; } = true;
    /// <summary>Maximum number of cached parsed models (0 disables caching).</summary>
    public int CacheSize { get; init; } = 0;
    /// <summary>Parse BPMN Diagram Interchange (shapes/edges) when true.</summary>
    public bool ParseDiagramInterchange { get; init; } = false;
    /// <summary>
    /// Roundtrip mode: Normalized (current default behaviour) vs Strict (attempt lossless preservation).
    /// </summary>
    public BpmnRoundtripMode RoundtripMode { get; init; } = BpmnRoundtripMode.Normalized;
    /// <summary>
    /// If true (default), parser applies memory optimizations in strict mode (release empty raw maps, selective cloning, interning IDs).
    /// </summary>
    public bool OptimizeStrictMemory { get; init; } = true;
    /// <summary>
    /// If true (default), short identifiers (id, sourceRef, targetRef) and flow refs are interned via a small in-memory pool to reduce duplicates.
    /// </summary>
    public bool InternIds { get; init; } = true;
    /// <summary>
    /// Strict-mode capture switch: record raw artifact elements (textAnnotation, association, group). Default true for backward compatibility.
    /// </summary>
    public bool CaptureArtifacts { get; init; } = true;
    /// <summary>
    /// Strict-mode capture switch: record raw Diagram Interchange root (BPMNDiagram parent) if present. Default true.
    /// </summary>
    public bool CaptureDiRaw { get; init; } = true;
}

/// <summary>
/// Roundtrip serialization strategy.
/// </summary>
public enum BpmnRoundtripMode
{
    /// <summary>Existing simplified model: extensions flattened, attributes normalized.</summary>
    Normalized = 0,
    /// <summary>Lossless: capture raw XML segments, namespaces, ordering (implemented in later phases).</summary>
    Strict = 1
}
