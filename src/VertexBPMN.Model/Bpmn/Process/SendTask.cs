using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Send task, as per Figure 10.14.
/// </summary>
public record SendTask(
    string? Implementation = null,
    Message? MessageRef = null,
    Operation? OperationRef = null
) : Task;