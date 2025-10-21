using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Message class, as per Figure 8.30.
/// </summary>
public record Message(
    string? Name = null,
    ItemDefinition? ItemRef = null
) : RootElement();