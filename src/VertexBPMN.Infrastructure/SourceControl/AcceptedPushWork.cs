using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedPushWork(RepositoryBinding Binding, long BindingRevision,
	PushCommand Command, AcceptedCommitWork LocalCommit, SourceControlOperationState State);

internal sealed record StoredRemotePush(int SchemaVersion, Guid RepositoryId, PushReceipt Receipt);

/// <summary>A complete rejection from the single push attempt proves no ref update for that attempt.</summary>
internal sealed class RejectedRemotePushException() : Exception("Remote push rejected.");
