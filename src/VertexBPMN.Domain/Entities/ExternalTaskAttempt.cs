namespace VertexBPMN.Domain.Entities;

public sealed class ExternalTaskAttempt
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int AttemptNumber { get; set; }
    public Guid LeaseId { get; set; }
    public long LeaseGeneration { get; set; }
    public string WorkerIssuer { get; set; } = string.Empty;
    public string WorkerSubject { get; set; } = string.Empty;
    public long StartedAt { get; set; }
    public long? EndedAt { get; set; }
    public string? EndReason { get; set; }
    public string? ErrorCode { get; set; }
    public Guid? FailureId { get; set; }
    public string? FailureHash { get; set; }
}
