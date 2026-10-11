using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record StoredLocalCommit(int SchemaVersion, Guid RepositoryId, long WorkspaceFence,
    CommitReceipt Receipt);
