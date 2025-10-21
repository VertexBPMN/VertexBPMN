using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Service task, as per Figure 10.12.
/// </summary>
public record ServiceTask(
    string? Implementation = null,
    Operation? OperationRef = null
) : Task;