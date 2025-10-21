using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Data object, as per Figure 10.51.
/// </summary>
public record DataObject(
    bool IsCollection = false
) : FlowElement, ItemAwareElement;