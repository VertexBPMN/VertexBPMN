namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Process task (5.4.9.2, inherits from Task).
/// Extension: Enhanced BPMN integration.
/// </summary>
public record ProcessTask(
    Process ProcessRef // Ref to BPMN Process.
) : Task();