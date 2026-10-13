namespace VertexBPMN.Domain.Entities;

/// <summary>Tenant-scoped configuration metadata; credential values are stored elsewhere.</summary>
public sealed class SourceControlConnectionProfileRecord
{
	public Guid Id { get; set; }
	public string TenantId { get; set; } = "";
	public long Revision { get; set; }
	public string ProfileJson { get; set; } = "";
	public bool IsRevoked { get; set; }
}
