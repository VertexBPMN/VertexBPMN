    using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record DataStore : BaseElement
{
    public string? Name { get; set; }
    public ItemDefinition? ItemSubjectRef { get; set; }
    public bool? IsUnlimited { get; set; }
    public int? Capacity { get; set; }
}