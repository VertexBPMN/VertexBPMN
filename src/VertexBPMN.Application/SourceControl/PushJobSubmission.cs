using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Publish a completed owned local commit, never a caller-selected object or branch.</summary>
public sealed record PushJobSubmission(Guid CommitOperationId, ExpectedRemoteRef ExpectedRemote);
