using VertexBPMN.Domain.Model.Bpmn.Common.Faults;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class ErrorEventDefinition : EventDefinition
{
    public Error? ErrorRef { get; set; }
}