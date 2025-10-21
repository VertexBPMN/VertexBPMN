using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Property, as per Figure 10.56.
/// </summary>
public record Property(
    string Name
) : ItemAwareElement
{
    public string Id { get; set; }
}