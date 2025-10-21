namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Business rule task, as per Figure 10.10.
/// </summary>
public record BusinessRuleTask(
    string? Implementation = null
) : Task();