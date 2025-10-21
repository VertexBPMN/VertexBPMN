using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Abstract artifact class, as per Figure 8.8.
/// </summary>
public abstract record Artifact() : FlowElement();