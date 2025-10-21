using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Data object reference, as per Figure 10.51.
/// </summary>
public record DataObjectReference(
    DataObject DataObjectRef
) : FlowElement, ItemAwareElement;