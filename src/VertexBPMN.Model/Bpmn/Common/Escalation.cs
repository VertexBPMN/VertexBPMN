using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Escalation class, as per Figure 8.19.
/// </summary>
public record Escalation(
    string? Name = null,
    string? EscalationCode = null,
    ItemDefinition? StructureRef = null
) : RootElement();