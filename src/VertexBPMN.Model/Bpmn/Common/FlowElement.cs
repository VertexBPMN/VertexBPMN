using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Abstract flow element, as per Figure 8.22.
/// </summary>
public abstract record FlowElement(
    string? Name = null,
    Auditing? Auditing = null,
    Monitoring? Monitoring = null,
    List<string> CategoryValueRef = null!
) : BaseElement();