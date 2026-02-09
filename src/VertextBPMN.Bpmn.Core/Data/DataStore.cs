using VertexBPMN.Domain.Model.Bpmn.Common.Items;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class DataStore : BaseElement
{
    public string? Name { get; set; }
    public ItemDefinition? ItemSubjectRef { get; set; }
    public bool? IsUnlimited { get; set; }
    public int? Capacity { get; set; }
}