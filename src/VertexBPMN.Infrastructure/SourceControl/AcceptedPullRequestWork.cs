using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedPullRequestWork(RepositoryBinding Binding, long BindingRevision,
    Guid PushOperationId, PullRequestCommand Command, SourceControlOperationState State);

internal sealed record StoredPullRequest(int SchemaVersion, Guid RepositoryId, PullRequestCommand Command,
    PullRequestReceipt? Receipt);
