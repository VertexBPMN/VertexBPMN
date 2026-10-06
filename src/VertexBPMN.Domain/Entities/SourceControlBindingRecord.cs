namespace VertexBPMN.Domain.Entities;

public sealed class SourceControlBindingRecord
{
	public Guid Id { get; set; }
	public string TenantId { get; set; } = "";
	public long Revision { get; set; }
	public string BindingJson { get; set; } = "";
	public string GrantsJson { get; set; } = "[]";
}
