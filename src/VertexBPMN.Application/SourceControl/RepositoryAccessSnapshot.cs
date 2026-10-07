using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

public sealed record RepositoryAccessSnapshot(RepositoryBinding Binding, long Revision,
	IReadOnlyList<RepositoryGrant> Grants);
