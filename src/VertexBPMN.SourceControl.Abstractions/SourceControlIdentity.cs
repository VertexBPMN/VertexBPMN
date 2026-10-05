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
            throw new ArgumentException("An explicit canonical tenant is required.", nameof(tenantId));
        if (actorId.Length > 512 || actorId != actorId.Trim() || actorId.Any(char.IsControl))
            throw new ArgumentException("A canonical authenticated actor is required.", nameof(actorId));
        TenantId = tenantId;
        ActorId = actorId;
    }
}

/// <summary>A complete Git object ID; no branch names, revision expressions or abbreviated IDs.</summary>
public sealed record GitCommitId
{
    public string Value { get; }

    public GitCommitId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length is not (40 or 64) || value.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A complete SHA-1 or SHA-256 Git object ID is required.", nameof(value));
        Value = value.ToLowerInvariant();
    }

    public override string ToString() => Value;
}

/// <summary>Stable operation key; storage must enforce uniqueness and bind it to the request digest.</summary>
public sealed record SourceControlIdempotencyKey
{
    public string Value { get; }

    public SourceControlIdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128 || value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("An ASCII operation key of at most 128 characters is required.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}

/// <summary>Null means the remote ref MUST NOT exist, never that the concurrency check is optional.</summary>
public sealed record ExpectedRemoteRef
{
    public GitCommitId? Commit { get; }
    public bool MustBeAbsent => Commit is null;

    private ExpectedRemoteRef(GitCommitId? commit) => Commit = commit;

    public static ExpectedRemoteRef Absent { get; } = new((GitCommitId?)null);

    public static ExpectedRemoteRef At(GitCommitId commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        return new(commit);
    }
}
