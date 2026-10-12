using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Trusted adapter session for durable orchestration, including read-only reconciliation after an uncertain create.</summary>
internal interface IRepositoryHostingAdapter
{
	string ProviderId { get; }
	Task<IRepositoryHostingSession> OpenAsync(SourceControlContext context, RepositoryBinding binding,
		IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken);
}
