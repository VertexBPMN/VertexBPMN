using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Service;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Process class, as per Figure 10.2 and 10.3.
/// </summary>
public record Process : CallableElement
{
    public ProcessType ProcessType { get; set; } = ProcessType.None;
    public bool IsExecutable { get; set; } = false;
    public bool IsClosed { get; set; } = false;
    public Auditing? Auditing { get; set; }
    public Monitoring? Monitoring { get; set; }
    public List<Property> Properties { get; set; } = [];
    public List<Artifact> Artifacts { get; set; } = [];
    public List<ResourceRole> Resources { get; set; } = [];
    public List<CorrelationSubscription> CorrelationSubscriptions { get; set; } = [];
    public List<Interface> Supports { get; set; } = [];
    public List<LaneSet>? LaneSets { get; set; } = [];
    public List<FlowElement>? FlowElements { get; set; } = [];
}