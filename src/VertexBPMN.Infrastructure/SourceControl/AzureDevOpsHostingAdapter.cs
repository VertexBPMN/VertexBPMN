using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class AzureDevOpsHostingAdapter(ProfiledHostingCredentialBroker credentials, GitHttpsPolicyRegistry policies,
	IOptions<SourceControlOptions> options) : IProfiledRepositoryHostingAdapter
{
	public string ProviderId => AzureDevOpsHostingTargetPolicy.ProviderId;
	public Task<IRepositoryHostingSession> OpenAsync(SourceControlContext context, RepositoryBinding binding,
		IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken) =>
		throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);

	public async Task<IRepositoryHostingSession> OpenProfiledAsync(SourceControlContext context, RepositoryBinding binding,
		AcceptedHostingExecution execution, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		var apiBinding = HostingHttpsContext.ApiBinding(binding, execution.HostingTarget);
		var apiProfile = HostingHttpsContext.ApiProfile(execution.Target.Profile);
		policies.DemandCurrent(context, apiBinding, apiProfile, execution.ApiPolicy);
		var network = await GitHttpsNetworkBoundary.ResolveAsync(context, apiBinding, apiProfile, execution.ApiPolicy.Network, cancellationToken);
		var approved = new ApprovedGitHttpsTarget(network.Remote, network.Profile, network.CopyAddresses()) { AdministrativePolicy = execution.ApiPolicy };
		var credential = await credentials.IssueAzureAsync(context, execution, approved, cancellationToken);
		try
		{ return new Session(execution.HostingTarget, credential, approved, options.Value.Limits.ReadTimeout); }
		catch { credential.Dispose(); throw; }
	}

	private sealed class Session : IRepositoryHostingSession
	{
		private readonly HttpClient client;
		private readonly HostingHttpCredentialLease credential;
		private readonly AzureDevOpsPullRequestTransport transport;
		internal Session(RepositoryHostingTarget target, HostingHttpCredentialLease credential, ApprovedGitHttpsTarget approved, TimeSpan timeout)
		{
			this.credential = credential;
			client = SourceControlHttps.CreateAzureDevOpsClient(approved, timeout);
			transport = new(client, target, credential);
		}
		public Task<PullRequestReceipt> CreateAsync(PullRequestCommand command, CancellationToken cancellationToken) => transport.CreateAsync(command, cancellationToken);
		public Task<PullRequestReceipt> ReconcileAsync(PullRequestCommand command, CancellationToken cancellationToken) => transport.ReconcileAsync(command, cancellationToken);
		public Task<PullRequestReceipt> ReadAsync(PullRequestCommand command, long number, CancellationToken cancellationToken) => transport.ReadAsync(command, number, cancellationToken);
		public Task<IReadOnlyList<PullRequestReview>> ReadReviewsAsync(long number, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			// Current UUID-keyed Azure votes are exposed separately; they are not GitHub review history.
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		public void Dispose() { client.Dispose(); credential.Dispose(); }
	}
}
