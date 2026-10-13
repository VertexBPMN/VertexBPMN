using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal interface IProfiledRepositoryHostingAdapter : IRepositoryHostingAdapter
{
	Task<IRepositoryHostingSession> OpenProfiledAsync(SourceControlContext context, RepositoryBinding binding,
		AcceptedHostingExecution execution, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
}
