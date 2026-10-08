using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Reads hosting state without mutating the original creation receipt or granting deployment approval.</summary>
public sealed class SourceControlPullRequestStatusReader
{
	private readonly PersistentSourceControlStore store;
	private readonly GitHubAppTokenBroker tokens;
	private readonly ISourceControlActorResolver actors;
	private readonly IOptions<SourceControlOptions> options;

	internal SourceControlPullRequestStatusReader(PersistentSourceControlStore store, GitHubAppTokenBroker tokens,
		ISourceControlActorResolver actors, IOptions<SourceControlOptions> options)
	{
		this.store = store;
		this.tokens = tokens;
		this.actors = actors;
		this.options = options;
	}
	public async Task<PullRequestReceipt> ReadAsync(SourceControlContext context, Guid operationId, CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		var confirmed = await store.ReadConfirmedPullRequestAsync(context, operationId, roles, cancellationToken);
		using var lease = await tokens.IssueAsync(context, confirmed.Binding.Id, roles,
			RepositoryPermission.Read, cancellationToken);
		using var client = SourceControlHttps.CreateClient(options.Value.AllowedHosts, options.Value.Limits.ReadTimeout);
		var transport = new GitHubPullRequestTransport(client);
		var receipt = await transport.ReadAsync(confirmed.Binding, confirmed.Command,
			confirmed.Receipt.Number, lease, cancellationToken);
		receipt = receipt with { Reviews = await transport.ReadReviewsAsync(confirmed.Binding,
			confirmed.Receipt.Number, lease, cancellationToken) };
		// A revocation during the remote read must not publish the result to the caller.
		await store.ReadConfirmedPullRequestAsync(context, operationId,
			await actors.ResolveAsync(context, cancellationToken), cancellationToken);
		return receipt;
	}
}
