namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Hosting-specific review operations, deliberately separate from Git transport.</summary>
public interface IRepositoryHostingProvider
{
    Task<SourceControlAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);
    Task<PullRequestReceipt> CreatePullRequestAsync(SourceControlContext context,
        RepositoryBinding binding, PullRequestCommand command, CancellationToken cancellationToken);
    Task<PullRequestReceipt> GetPullRequestAsync(SourceControlContext context,
        RepositoryBinding binding, long number, CancellationToken cancellationToken);
    Task<ReleaseEvidence?> VerifyReleaseAsync(SourceControlContext context,
        RepositoryBinding binding, GitCommitId commit, CancellationToken cancellationToken);
}
