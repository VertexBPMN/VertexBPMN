namespace VertexBPMN.Domain.Model.Bpmn;

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