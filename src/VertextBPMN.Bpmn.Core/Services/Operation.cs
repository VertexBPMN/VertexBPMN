using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Faults;
using VertexBPMN.Domain.Model.Bpmn.Common.Messages;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Services;

public class Operation : BaseElement
{
    public required string Name { get; set; }
    public string? ImplementationRef { get; set; }
    public required Message InMessageRef { get; set; }
    public Message? OutMessageRef { get; set; }
    public IReadOnlyList<Error> ErrorRefs { get; } = [];
}