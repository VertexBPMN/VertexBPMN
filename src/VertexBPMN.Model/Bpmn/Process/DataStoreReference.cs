using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Data store reference, as per Figure 10.55.
/// </summary>
public record DataStoreReference(
    DataStore DataStoreRef
) : FlowElement, ItemAwareElement;