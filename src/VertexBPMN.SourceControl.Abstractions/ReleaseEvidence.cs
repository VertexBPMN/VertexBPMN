namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Evidence returned only by the trusted hosting adapter, never accepted as client-supplied proof.
/// The application revalidates permissions and evidence before deployment.
/// </summary>
public sealed record ReleaseEvidence(Guid RepositoryId, string ProviderId, long PullRequestNumber,
    string ReleaseBranch, GitCommitId HeadCommit, GitCommitId MergeCommit,
    DateTimeOffset MergedAt, DateTimeOffset VerifiedAt, string ProtectionFingerprint);
