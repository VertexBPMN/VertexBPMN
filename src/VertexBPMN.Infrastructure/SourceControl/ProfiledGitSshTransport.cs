using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitSshTransport(PersistentSourceControlStore store, ISourceControlActorResolver actors,
	GitSshPolicyRegistry policies, ProfiledSshCredentialBroker credentials, ControlledGitProcess git, IOptions<SourceControlOptions> options)
{
	internal async Task<PreparedSshRead> PrepareClaimedReadAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken cancellationToken)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			var work = await ReadCurrentAsync(context, id, worker, fence, deadline.Token);
			var target = work.Execution.Target;
			var binding = (await store.FindAsync(context.TenantId, target.RepositoryId, deadline.Token))?.Binding
				?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
			var approved = await GitSshNetworkBoundary.ResolveAsync(context, binding, target,
				work.Execution.Policy.Network, work.Execution.Policy.Trust, resolve, deadline.Token);
			DemandSameWork(work, await ReadCurrentAsync(context, id, worker, fence, deadline.Token));
			return new(work, approved);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
	}

	private async Task<AcceptedGitSshReadWork> ReadCurrentAsync(SourceControlContext context, Guid id, string worker,
		long fence, CancellationToken cancellationToken) => await store.ReadClaimedSshReadAsync(context, id, worker, fence,
			await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken);

	private static void DemandSameWork(AcceptedGitSshReadWork expected, AcceptedGitSshReadWork current)
	{
		if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(current))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}

	internal sealed record PreparedSshRead(AcceptedGitSshReadWork Work, ApprovedGitSshTarget Approved);
}
