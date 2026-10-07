using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedCommitWork(RepositoryBinding Binding, long BindingRevision,
    CommitCommand Command, DateTimeOffset AcceptedAt, SourceControlOperationState State);

internal sealed record StoredLocalCommit(int SchemaVersion, Guid RepositoryId, long WorkspaceFence,
    CommitReceipt Receipt);
