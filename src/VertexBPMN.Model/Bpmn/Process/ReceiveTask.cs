using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Receive task, as per Figure 10.14.
/// </summary>
public record ReceiveTask(
    string? Implementation = null,
    bool Instantiate = false,
    Message? MessageRef = null,
    Operation? OperationRef = null
) : Task;