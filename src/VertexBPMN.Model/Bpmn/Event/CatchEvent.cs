using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Abstract catch event, as per Figure 10.69.
/// </summary>
public abstract record CatchEvent : Event {
   public bool ParallelMultiple { get; init; } = false;
   public List<DataOutput> DataOutputs { get; set; } = null!;
   public List<DataOutputAssociation> DataOutputAssociations { get; set; } = [];
   public OutputSet? OutputSet { get; set; } = null;
   public List<EventDefinition> EventDefinitions { get; set; } = [];
   public List<EventDefinition> EventDefinitionRefs { get; set; } = [];
};