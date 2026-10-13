using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Dispatch supported versioned reads and durable writes. Uncertain writes are reconciled, never blindly replayed.</summary>
internal sealed class SourceControlJobsHostedService(IServiceScopeFactory scopes,
	ILogger<SourceControlJobsHostedService> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
		do
		{
			try
			{
				await using var scope = scopes.CreateAsyncScope();
				var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
				var now = DateTimeOffset.UtcNow;
				var retryBefore = now.AddMinutes(-1).UtcTicks;
				var jobs = await db.SourceControlOperations.AsNoTracking().Where(x =>
					(((x.Kind == (int)SourceControlOperationKind.Commit || x.Kind == (int)SourceControlOperationKind.Push
						|| x.Kind == (int)SourceControlOperationKind.PullRequest)
						&& x.ExecutionContractVersion == 0 && x.ProtectedExecutionTarget == null)
						|| ((x.Kind == (int)SourceControlOperationKind.Read || x.Kind == (int)SourceControlOperationKind.Commit
							|| x.Kind == (int)SourceControlOperationKind.Push) && x.ExecutionContractVersion == AcceptedGitHttpsExecution.ContractVersion
							&& x.ProtectedExecutionTarget != null)
						|| (x.Kind == (int)SourceControlOperationKind.Read
							&& x.ExecutionContractVersion == AcceptedGitSshExecution.ContractVersion && x.ProtectedExecutionTarget != null)
						|| (x.Kind == (int)SourceControlOperationKind.PullRequest
							&& x.ExecutionContractVersion == AcceptedHostingExecution.ContractVersion && x.ProtectedExecutionTarget != null))
					&& (x.State == (int)SourceControlOperationState.Queued
						|| (x.State == (int)SourceControlOperationState.ResultUnknown && x.UpdatedUtcTicks <= retryBefore)
						|| ((x.Kind == (int)SourceControlOperationKind.PullRequest || x.Kind == (int)SourceControlOperationKind.Read)
							&& x.LeaseUntilUtcTicks <= now.UtcTicks
							&& (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))))
					.OrderBy(x => x.UpdatedUtcTicks).Take(8).ToArrayAsync(stoppingToken);
				foreach (var job in jobs)
				{
					await DispatchJobAsync(job, stoppingToken);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
			catch (Exception) { logger.LogWarning("Source-control dispatch failed; durable operations remain available for recovery."); }
		} while (await timer.WaitForNextTickAsync(stoppingToken));
	}

	private async Task DispatchJobAsync(VertexBPMN.Domain.Entities.SourceControlOperationRecord job, CancellationToken stoppingToken)
	{
		try
		{
			await using var scope = scopes.CreateAsyncScope();
			var context = new SourceControlContext(job.TenantId, job.ActorId);
			var worker = "host-" + Guid.NewGuid().ToString("N");
			var kind = (SourceControlOperationKind)job.Kind;
			var fence = await ClaimAsync(scope.ServiceProvider, context, job.Id, kind, job.ExecutionContractVersion, worker, stoppingToken);
			if (fence is not null)
			{
				await DispatchAsync(scope.ServiceProvider, context, job.Id, kind, job.ExecutionContractVersion, worker, fence.Value, stoppingToken);
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
		catch (SourceControlSecurityException error)
		{
			await using var scope = scopes.CreateAsyncScope();
			await scope.ServiceProvider.GetRequiredService<PersistentSourceControlStore>()
				.RejectUnstartedProfiledDispatchAsync(job, error.Code, stoppingToken);
		}
		catch (Exception)
		{
			logger.LogWarning("Source-control operation dispatch failed; other queued operations continue and durable recovery remains available.");
		}
	}

	private static async Task<long?> ClaimAsync(IServiceProvider services, SourceControlContext context, Guid id,
		SourceControlOperationKind kind, int version, string worker, CancellationToken cancellationToken)
	{
		var store = services.GetRequiredService<PersistentSourceControlStore>();
		if (version == 0)
		{
			return await store.TryClaimAsync(context.TenantId, id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), cancellationToken);
		}
		var roles = await services.GetRequiredService<ISourceControlActorResolver>().ResolveAsync(context, cancellationToken);
		if (version == AcceptedGitSshExecution.ContractVersion && kind == SourceControlOperationKind.Read)
		{
			var policies = services.GetRequiredService<GitSshPolicyRegistry>();
			var work = await store.ReadAcceptedSshReadAsync(context, id, roles, policies, cancellationToken);
			if (work.Request.Query is null)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			return await store.TryClaimSshReadAsync(context, id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), roles, policies, cancellationToken);
		}
		if (version == AcceptedHostingExecution.ContractVersion && kind == SourceControlOperationKind.PullRequest)
		{
			return await store.TryClaimHostingAsync(context, id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), roles, cancellationToken);
		}
		return kind == SourceControlOperationKind.Read
			? await store.TryClaimHttpsRevisionReadAsync(context, id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2),
				roles, services.GetRequiredService<GitHttpsPolicyRegistry>(), cancellationToken)
			: await store.TryClaimProfiledWriteAsync(context, id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), roles, cancellationToken);
	}

	private static Task DispatchAsync(IServiceProvider services, SourceControlContext context, Guid id,
		SourceControlOperationKind kind, int version, string worker, long fence, CancellationToken cancellationToken)
	{
		if (version == AcceptedGitSshExecution.ContractVersion && kind == SourceControlOperationKind.Read)
		{
			return services.GetRequiredService<ProfiledSshReadService>().RunClaimedAsync(context, id, worker, fence, cancellationToken);
		}
		var actors = services.GetRequiredService<ISourceControlActorResolver>();
		return kind switch
		{
			SourceControlOperationKind.Read => services.GetRequiredService<ProfiledGitReadService>()
				.RunClaimedAsync(context, id, worker, fence, cancellationToken),
			SourceControlOperationKind.Commit => services.GetRequiredService<SourceControlClaimedCommitRunner>()
				.RunAsync(context, id, worker, fence, token => actors.ResolveAsync(context, token), cancellationToken),
			SourceControlOperationKind.Push => services.GetRequiredService<SourceControlClaimedPushRunner>()
				.RunAsync(context, id, worker, fence, token => actors.ResolveAsync(context, token), cancellationToken),
			_ => services.GetRequiredService<SourceControlClaimedPullRequestRunner>()
				.RunAsync(context, id, worker, fence, token => actors.ResolveAsync(context, token), cancellationToken)
		};
	}
}
