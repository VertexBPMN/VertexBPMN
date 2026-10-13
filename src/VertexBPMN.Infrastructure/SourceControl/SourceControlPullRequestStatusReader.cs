using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Reads hosting state without mutating the original creation receipt or granting deployment approval.</summary>
public sealed class SourceControlPullRequestStatusReader
{
	private readonly PersistentSourceControlStore store;
	private readonly ISourceControlActorResolver actors;
	private readonly RepositoryHostingRegistry hosting;

	internal SourceControlPullRequestStatusReader(PersistentSourceControlStore store, GitHubAppTokenBroker tokens,
		ISourceControlActorResolver actors, IOptions<SourceControlOptions> options, RepositoryHostingRegistry? hosting = null)
	{
		this.store = store;
		this.actors = actors;
		this.hosting = hosting ?? new RepositoryHostingRegistry([new GitHubHostingAdapter(tokens, options)]);
	}
	public async Task<PullRequestReceipt> ReadAsync(SourceControlContext context, Guid operationId, CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		var confirmed = await store.ReadConfirmedPullRequestAsync(context, operationId, roles, cancellationToken);
		using var session = confirmed.Hosting is null
			? await hosting.OpenLegacyAsync(context, confirmed.Binding, roles, RepositoryPermission.Read, cancellationToken)
			: await hosting.OpenProfiledAsync(context, confirmed.Binding, confirmed.Hosting, roles, cancellationToken);
		var receipt = await session.ReadAsync(confirmed.Command, confirmed.Receipt.Number, cancellationToken);
		RepositoryHostingReceiptPolicy.Demand(confirmed.Hosting?.HostingTarget ?? RepositoryHostingTargetPolicy.LegacyGitHub(confirmed.Binding), confirmed.Command, receipt);
		if (receipt.Number != confirmed.Receipt.Number)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		if (receipt.ProviderId != AzureDevOpsHostingTargetPolicy.ProviderId)
		{
			receipt = receipt with { Reviews = await session.ReadReviewsAsync(confirmed.Receipt.Number, cancellationToken) };
		}
		// A revocation during the remote read must not publish the result to the caller.
		await store.ReadConfirmedPullRequestAsync(context, operationId,
			await actors.ResolveAsync(context, cancellationToken), cancellationToken);
		return receipt;
	}
}
