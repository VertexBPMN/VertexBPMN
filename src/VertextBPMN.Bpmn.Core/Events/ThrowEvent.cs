using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Events;
using VertexBPMN.Domain.Model.Bpmn.Data;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public abstract class ThrowEvent : Event
{
    public IReadOnlyList<EventDefinition> EventDefinitions { get; } = [];
    public IReadOnlyList<DataAssociation> DataInputAssociations { get; } = [];
    public IReadOnlyList<DataInput> DataInputs { get; } = [];
}