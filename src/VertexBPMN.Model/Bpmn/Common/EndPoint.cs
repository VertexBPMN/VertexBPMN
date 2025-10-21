using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

/// <summary>
/// Endpoint stub referenced by Participant.
/// </summary>
public record EndPoint(string Address) : BaseElement;