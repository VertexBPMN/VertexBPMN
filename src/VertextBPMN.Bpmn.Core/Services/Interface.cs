using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Services;

public class Interface : RootElement
{
    public required string Name { get; set; }
    public string? ImplementationRef { get; set; }
    public IReadOnlyList<Operation> Operations { get; } = [];
    public IReadOnlyList<CallableElement> CallableElements { get; } = [];
}