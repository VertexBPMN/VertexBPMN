namespace VertexBPMN.Domain.Entities.Debugging;

using VertexBPMN.Domain.Entities;

/// <summary>
/// Result of one persisted visual-debug step.
/// </summary>
public sealed class VisualDebugStepResult
{
    public Guid ProcessInstanceId { get; init; }
    public Guid TokenId { get; init; }
    public string StartActivityId { get; init; } = string.Empty;
    public string EndActivityId { get; init; } = string.Empty;
    public string EndNodeType { get; init; } = string.Empty;
    public bool ProcessCompleted { get; init; }
    public DateTime Timestamp { get; init; }
    public ProcessInstance Instance { get; init; } = null!;
}
