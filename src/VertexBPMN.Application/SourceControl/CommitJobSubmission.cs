using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Confirmed editor snapshot, not arbitrary serialized job bytes or a client-supplied operation ID.</summary>
public sealed record CommitJobSubmission(Guid SessionId, long SessionRevision, GitCommitId BaseCommit,
    string WorkBranch, string Message, IReadOnlyList<ModelSnapshot> Snapshots);
