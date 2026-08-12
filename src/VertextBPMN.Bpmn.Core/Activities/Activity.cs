using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;
using VertexBPMN.Domain.Model.Bpmn.Data;
using VertexBPMN.Domain.Model.Bpmn.Events;

namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public abstract class Activity : FlowNode
{
    public bool? IsForCompensation { get; set; }
    public int? StartQuantity { get; set; }
    public int? CompletionQuantity { get; set; }
    public LoopCharacteristics? LoopCharacteristics { get; set; }
    public IOSpecification? IoSpecification { get; set; }
    public IReadOnlyList<DataAssociation> DataInputAssociations { get; } = [];
    public IReadOnlyList<DataAssociation> DataOutputAssociations { get; } = [];
    public IReadOnlyList<BoundaryEvent> BoundaryEvents { get; } = [];
}