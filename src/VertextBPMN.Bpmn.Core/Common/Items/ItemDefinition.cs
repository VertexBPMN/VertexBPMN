using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Items;

public class ItemDefinition : RootElement
{
    public ItemKind ItemKind { get; set; } = ItemKind.Information;
    public string? StructureRef { get; set; }
    public bool IsCollection { get; set; } = false;
    public Import? Import { get; set; }
}