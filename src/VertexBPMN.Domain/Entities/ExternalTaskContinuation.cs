namespace VertexBPMN.Domain.Entities;

public sealed class ExternalTaskContinuation
{
	public Guid Id { get; set; }
	public Guid JobId { get; set; }
	public Guid ActivityExecutionId { get; set; }
	public ExternalTaskOutcome Outcome { get; set; }
	public ExternalTaskContinuationState State { get; set; }
	public long Revision { get; set; }
	public long CreatedAt { get; set; }
	public long? AppliedAt { get; set; }
}
