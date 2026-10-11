using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedCommitWork(RepositoryBinding Binding, long BindingRevision,
    CommitCommand Command, DateTimeOffset AcceptedAt, SourceControlOperationState State);
