using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Item definition, as per Figure 8.25.
/// </summary>
public record ItemDefinition(
    ItemKind ItemKind = ItemKind.Information,
    string? StructureRef = null,
    bool IsCollection = false,
    Import? Import = null
) : RootElement();