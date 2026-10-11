using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record StoredPullRequest(int SchemaVersion, Guid RepositoryId, PullRequestCommand Command,
    PullRequestReceipt? Receipt);
