using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Service;

#nullable enable

/// <summary>
/// Interface class, as per Figure 8.36.
/// </summary>
public record Interface(
    string Name,
    List<Operation> Operations,
    List<CallableElement> CallableElements = null!
) : RootElement()
{
    public string? ImplementationRef { get; set; }
}