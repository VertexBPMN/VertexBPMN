using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class GitHubHostingAdapter(GitHubAppTokenBroker tokens, IOptions<SourceControlOptions> options,
	ProfiledHostingCredentialBroker? profiledTokens = null, GitHttpsPolicyRegistry? policies = null) : IProfiledRepositoryHostingAdapter
{
	public string ProviderId => RepositoryHostingRegistry.LegacyGitHubProviderId;

	public async Task<IRepositoryHostingSession> OpenProfiledAsync(SourceControlContext context, RepositoryBinding binding,
		AcceptedHostingExecution execution, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		if (profiledTokens is null || policies is null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var apiBinding = HostingHttpsContext.ApiBinding(binding, execution.HostingTarget);
		var apiProfile = HostingHttpsContext.ApiProfile(execution.Target.Profile);
		policies.DemandCurrent(context, apiBinding, apiProfile, execution.ApiPolicy);
		var network = await GitHttpsNetworkBoundary.ResolveAsync(context, apiBinding, apiProfile, execution.ApiPolicy.Network, cancellationToken);
		var approved = new ApprovedGitHttpsTarget(network.Remote, network.Profile, network.CopyAddresses()) { AdministrativePolicy = execution.ApiPolicy };
		var lease = await profiledTokens.IssueGitHubAsync(context, execution, approved, cancellationToken);
		try
		{
			return new GitHubHostingSession(binding, lease, options.Value, approved);
		}
		catch { lease.Dispose(); throw; }
	}

	public async Task<IRepositoryHostingSession> OpenAsync(SourceControlContext context, RepositoryBinding binding,
		IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken)
	{
		if (binding.ConnectionProfileId is not null || permission is not (RepositoryPermission.Read or RepositoryPermission.PullRequest))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var lease = await tokens.IssueAsync(context, binding.Id, roles, permission, cancellationToken);
		try
		{
			return new GitHubHostingSession(binding, lease, options.Value);
		}
		catch
		{
			lease.Dispose();
			throw;
		}
	}

	private sealed class GitHubHostingSession : IRepositoryHostingSession
	{
		private readonly RepositoryBinding binding;
		private readonly GitHubTokenLease lease;
		private readonly HttpClient client;
		private readonly GitHubPullRequestTransport transport;

		internal GitHubHostingSession(RepositoryBinding binding, GitHubTokenLease lease, SourceControlOptions configuration,
			ApprovedGitHttpsTarget? approved = null)
		{
			this.binding = binding;
			this.lease = lease;
			client = approved is null
				? SourceControlHttps.CreateClient(configuration.AllowedHosts, configuration.Limits.ReadTimeout)
				: SourceControlHttps.CreateProfiledGitHubClient(approved, configuration.Limits.ReadTimeout);
			transport = new(client);
		}
		public Task<PullRequestReceipt> CreateAsync(PullRequestCommand command, CancellationToken cancellationToken) =>
			transport.CreateAsync(binding, command, lease, cancellationToken);
		public Task<PullRequestReceipt> ReconcileAsync(PullRequestCommand command, CancellationToken cancellationToken) =>
			transport.ReconcileAsync(binding, command, lease, cancellationToken);
		public Task<PullRequestReceipt> ReadAsync(PullRequestCommand command, long number, CancellationToken cancellationToken) =>
			transport.ReadAsync(binding, command, number, lease, cancellationToken);
		public Task<IReadOnlyList<PullRequestReview>> ReadReviewsAsync(long number, CancellationToken cancellationToken) =>
			transport.ReadReviewsAsync(binding, number, lease, cancellationToken);
		public void Dispose()
		{
			client.Dispose();
			lease.Dispose();
		}
	}
}
