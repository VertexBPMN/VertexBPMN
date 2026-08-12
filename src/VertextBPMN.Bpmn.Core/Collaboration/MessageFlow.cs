using VertexBPMN.Domain.Model.Bpmn.Common.Messages;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class MessageFlow : BaseElement
{
    public string? Name { get; set; }
    public BaseElement? SourceRef { get; set; }
    public BaseElement? TargetRef { get; set; }
    public Message? MessageRef { get; set; }
}