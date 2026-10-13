namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Pure hosting identity resolution; does not release credentials or perform remote I/O.</summary>
public interface IRepositoryHostingTargetResolver
{
	RepositoryHostingTarget Resolve(SourceControlContext context, RepositoryBinding binding, RepositoryConnectionProfile profile);
}
