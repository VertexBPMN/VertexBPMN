using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Signal class, as per Figure 10.93.
/// </summary>
public record Signal(
    string? Name = null,
    ItemDefinition? StructureRef = null
) : RootElement();