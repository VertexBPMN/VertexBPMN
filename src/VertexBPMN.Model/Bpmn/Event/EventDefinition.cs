using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Abstract event definition, as per Figure 10.73.
/// </summary>
public abstract record EventDefinition() : RootElement;