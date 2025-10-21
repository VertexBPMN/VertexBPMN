using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Resource parameter, as per Figure 8.31.
/// </summary>
public record ResourceParameter(
    string? Name = null,
    ItemDefinition? Type = null,
    bool IsRequired = false
) : BaseElement();