using System.Collections.Generic;

namespace VertexBPMN.Domain.Modeling;

public record BpmnSequenceFlow(string Id, string SourceRef, string TargetRef)
{
    /// <summary>
    /// Additional attributes for extensibility.
    /// </summary>
    public IDictionary<string, object> Attributes { get; init; } = new Dictionary<string, object>();
}