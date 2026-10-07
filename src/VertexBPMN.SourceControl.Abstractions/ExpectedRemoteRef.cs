namespace VertexBPMN.SourceControl.Abstractions;

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
