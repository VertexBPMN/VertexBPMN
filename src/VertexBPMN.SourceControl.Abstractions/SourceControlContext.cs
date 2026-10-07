namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Trusted host context, not an authorization grant or a client request DTO.
/// The host must construct this only after authentication and repository ACL checks.
/// </summary>
public sealed record SourceControlContext
{
    public string TenantId { get; }
    public string ActorId { get; }

    public SourceControlContext(string tenantId, string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        if (tenantId.Length > 64 || tenantId != tenantId.Trim() || tenantId == "$global"
            || tenantId.Any(char.IsControl))
		{
			throw new ArgumentException("An explicit canonical tenant is required.", nameof(tenantId));
		}

		if (actorId.Length > 512 || actorId != actorId.Trim() || actorId.Any(char.IsControl))
		{
			throw new ArgumentException("A canonical authenticated actor is required.", nameof(actorId));
		}

		TenantId = tenantId;
        ActorId = actorId;
    }
}
