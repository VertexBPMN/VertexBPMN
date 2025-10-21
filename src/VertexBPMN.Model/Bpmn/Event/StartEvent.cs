using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Start event, as per Figure 10.69.
/// </summary>
public record StartEvent(
    bool IsInterrupting = true
) : CatchEvent();