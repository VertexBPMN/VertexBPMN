namespace VertexBPMN.Domain.Entities;

public sealed class SourceControlSessionRecord
{
	public Guid Id { get; set; }
	public Guid RepositoryId { get; set; }
	public string TenantId { get; set; } = "";
	public string ActorId { get; set; } = "";
	public Guid DocumentGeneration { get; set; }
	public long Revision { get; set; }
	public string BaseCommit { get; set; } = "";
	public long ExpiresUtcTicks { get; set; }
	public string ProtectedSnapshots { get; set; } = "";
}
