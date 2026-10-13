using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class SourceControlPullRequestExecutor(PersistentSourceControlStore store,
	GitHubAppTokenBroker tokens, IOptions<SourceControlOptions> options, RepositoryHostingRegistry? hosting = null)
{
	internal async Task<PullRequestReceipt> ExecuteAsync(SourceControlContext context, Guid id, string worker,
		long fence, Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveRoles, CancellationToken cancellationToken)
	{
		var roles = await resolveRoles(cancellationToken);
		var work = await store.ReadPullRequestWorkAsync(context, id, worker, fence, roles, cancellationToken);
		var registry = hosting ?? new RepositoryHostingRegistry([new GitHubHostingAdapter(tokens, options)]);
		using var session = work.Hosting is null
			? await registry.OpenLegacyAsync(context, work.Binding, roles, RepositoryPermission.PullRequest, cancellationToken)
			: await registry.OpenProfiledAsync(context, work.Binding, work.Hosting, roles, cancellationToken);
		return await ExecutePreparedAsync(context, id, worker, fence, resolveRoles,
			(current, token) => session.CreateAsync(current.Command, token),
			(current, token) => session.ReconcileAsync(current.Command, token), cancellationToken);
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
			if (saved is null)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}

			try
			{
				var intent = JsonSerializer.Deserialize<StoredPullRequest>(saved);
				if (intent is null || intent.SchemaVersion != (work.Hosting is null ? 1 : 2) || intent.RepositoryId != work.Binding.Id
					|| intent.Command != work.Command || intent.Receipt is not null || intent.HostingTarget != work.Hosting?.HostingTarget)
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
