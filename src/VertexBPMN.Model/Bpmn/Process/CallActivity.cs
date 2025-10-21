namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Call activity, as per Figure 10.42.
/// </summary>
public record CallActivity(
    CallableElement? CalledElementRef = null
) : Activity();