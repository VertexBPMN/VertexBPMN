using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public abstract record ItemAwareElement : BaseElement
{
    public ItemDefinition? ItemSubjectRef { get; set; }
    public DataState? DataState { get; set; }
}