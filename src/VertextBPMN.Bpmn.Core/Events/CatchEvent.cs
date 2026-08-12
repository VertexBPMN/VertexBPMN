using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Events;
using VertexBPMN.Domain.Model.Bpmn.Data;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public abstract class CatchEvent : Event
{
    public IReadOnlyList<EventDefinition> EventDefinitions { get; } = [];
    public IReadOnlyList<DataAssociation> DataOutputAssociations { get; } = [];
    public IReadOnlyList<DataOutput> DataOutputs { get; } = [];
    public bool? ParallelMultiple { get; set; }
}