using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class SourceControlPullRequestExecutor(PersistentSourceControlStore store,
	GitHubAppTokenBroker tokens, IOptions<SourceControlOptions> options)
{
	internal async Task<PullRequestReceipt> ExecuteAsync(SourceControlContext context, Guid id, string worker,
		long fence, Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveRoles, CancellationToken cancellationToken)
	{
		var roles = await resolveRoles(cancellationToken);
		var work = await store.ReadPullRequestWorkAsync(context, id, worker, fence, roles, cancellationToken);
		using var lease = await tokens.IssueAsync(context, work.Binding.Id, roles, RepositoryPermission.PullRequest, cancellationToken);
		using var client = SourceControlHttps.CreateClient(options.Value.AllowedHosts, options.Value.Limits.ReadTimeout);
		var transport = new GitHubPullRequestTransport(client);
		return await ExecutePreparedAsync(context, id, worker, fence, resolveRoles,
			(current, token) => transport.CreateAsync(current.Binding, current.Command, lease, token),
			(current, token) => transport.ReconcileAsync(current.Binding, current.Command, lease, token), cancellationToken);
	}

	internal async Task<PullRequestReceipt> ExecutePreparedAsync(SourceControlContext context, Guid id, string worker,
		long fence, Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveRoles,
		Func<AcceptedPullRequestWork, CancellationToken, Task<PullRequestReceipt>> create,
		Func<AcceptedPullRequestWork, CancellationToken, Task<PullRequestReceipt>> reconcile, CancellationToken cancellationToken)
	{
		var roles = await resolveRoles(cancellationToken);
		var work = await store.ReadPullRequestWorkAsync(context, id, worker, fence, roles, cancellationToken);
		var saved = await store.ReadSavedResultAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, cancellationToken);
		var firstAttempt = saved is null && work.State == SourceControlOperationState.Running;
		if (firstAttempt)
		{
			if (!await store.SavePullRequestIntentAsync(context, id, worker, fence, roles, cancellationToken))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}
		}
		else
		{
			if (saved is null) throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			try
			{
				var intent = JsonSerializer.Deserialize<StoredPullRequest>(saved);
				if (intent is null || intent.SchemaVersion != 1 || intent.RepositoryId != work.Binding.Id
					|| intent.Command != work.Command || intent.Receipt is not null)
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
				}
			}
			catch (Exception error) when (error is JsonException or ArgumentException)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
		roles = await resolveRoles(cancellationToken);
		var receipt = await store.InvokePullRequestRemoteAsync(context, id, worker, fence, roles,
			firstAttempt ? create : reconcile, cancellationToken);
		roles = await resolveRoles(cancellationToken);
		await store.CompletePullRequestAsync(context, id, worker, fence, roles, receipt, cancellationToken);
		return receipt;
	}
}
