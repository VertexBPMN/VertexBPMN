using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record StoredRemotePush(int SchemaVersion, Guid RepositoryId, PushReceipt Receipt);
