using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedPushWork(RepositoryBinding Binding, long BindingRevision,
	PushCommand Command, AcceptedCommitWork LocalCommit, SourceControlOperationState State);
