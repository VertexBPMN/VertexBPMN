using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

public interface ISourceControlAccessStore
{
	Task<RepositoryAccessSnapshot?> FindAsync(string tenantId, Guid repositoryId, CancellationToken cancellationToken);
}
