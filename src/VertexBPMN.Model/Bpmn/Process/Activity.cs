using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Event;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Abstract activity, as per Figure 10.6.
/// </summary>
public abstract record Activity(
    bool IsForCompensation = false,
    int LoopCardinality = 0,
    List<ResourceRole> Resources = null!,
    SequenceFlow? Default = null,
    InputOutputSpecification? IoSpecification = null,
    List<Property> Properties = null!,
    List<DataInputAssociation> DataInputAssociations = null!,
    List<DataOutputAssociation> DataOutputAssociations = null!,
    LoopCharacteristics? LoopCharacteristics = null,
    CompletionQuantity CompletionQuantity = CompletionQuantity.One,
    StartQuantity StartQuantity = StartQuantity.One,
    List<BoundaryEvent> BoundaryEventRefs = null!
) : FlowNode;