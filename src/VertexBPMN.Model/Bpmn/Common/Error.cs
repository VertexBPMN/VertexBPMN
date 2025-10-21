using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Error class, as per Figure 8.18.
/// </summary>
public record Error(
    string? Name = null,
    string? ErrorCode = null,
    ItemDefinition? StructureRef = null
) : RootElement();