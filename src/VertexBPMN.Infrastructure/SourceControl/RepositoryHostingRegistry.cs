using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Fixed host registrations; unknown providers never fall back to a different hosting service.</summary>
internal sealed class RepositoryHostingRegistry
{
	internal const string LegacyGitHubProviderId = "github";
	private readonly Dictionary<string, IRepositoryHostingAdapter> adapters;

	public RepositoryHostingRegistry(IEnumerable<IRepositoryHostingAdapter> registrations)
	{
		adapters = registrations.ToDictionary(adapter => adapter.ProviderId, StringComparer.Ordinal);
	}

	internal IRepositoryHostingAdapter Resolve(string providerId) => adapters.TryGetValue(providerId, out var adapter)
		? adapter : throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);

	internal Task<IRepositoryHostingSession> OpenProfiledAsync(SourceControlContext context, RepositoryBinding binding,
		AcceptedHostingExecution execution, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		if (binding.ConnectionProfileId != execution.Target.Profile.Id
			|| binding.Id != execution.Target.RepositoryId || binding.TenantId != context.TenantId
			|| Resolve(execution.HostingTarget.ProviderId) is not IProfiledRepositoryHostingAdapter adapter)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		return adapter.OpenProfiledAsync(context, binding, execution, roles, cancellationToken);
	}

	internal Task<IRepositoryHostingSession> OpenLegacyAsync(SourceControlContext context, RepositoryBinding binding,
		IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken)
	{
		if (binding.ConnectionProfileId is not null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		return Resolve(LegacyGitHubProviderId).OpenAsync(context, binding, roles, permission, cancellationToken);
	}
}
