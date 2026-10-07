namespace VertexBPMN.Domain.Entities;

/// <summary>Durable idempotency receipt; never delete merely because job details expire.</summary>
public sealed class SourceControlOperationRecord
{
	public Guid Id { get; set; }
	public Guid RepositoryId { get; set; }
	public string TenantId { get; set; } = "";
	public string ActorId { get; set; } = "";
	public int Kind { get; set; }
	public string IdempotencyKey { get; set; } = "";
	public string RequestHash { get; set; } = "";
	public string ProtectedRequest { get; set; } = "";
	public int State { get; set; }
	public long UpdatedUtcTicks { get; set; }
	public long Fence { get; set; }
	public string? LeaseOwner { get; set; }
	public long? LeaseUntilUtcTicks { get; set; }
	public int? ErrorCode { get; set; }
	public string? ProtectedResult { get; set; }
}
