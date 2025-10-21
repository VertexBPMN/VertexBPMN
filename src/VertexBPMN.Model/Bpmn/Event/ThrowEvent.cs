using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Abstract throw event, as per Figure 10.69.
/// </summary>
public abstract record ThrowEvent(
    List<DataInput> DataInputs = null!,
    List<DataInputAssociation> DataInputAssociations = null!,
    InputSet? InputSet = null,
    List<EventDefinition> EventDefinitions = null!,
    List<EventDefinition> EventDefinitionRefs = null!
) : Event;