using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Dispatch only durable writes; uncertainty is handled by the existing read-only reconciliation path.</summary>
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
					(x.Kind == (int)SourceControlOperationKind.Commit || x.Kind == (int)SourceControlOperationKind.Push)
					&& (x.State == (int)SourceControlOperationState.Queued
						|| x.State == (int)SourceControlOperationState.ResultUnknown && x.UpdatedUtcTicks <= retryBefore))
					.OrderBy(x => x.UpdatedUtcTicks).Take(8).ToArrayAsync(stoppingToken);
				foreach (var job in jobs)
				{
					var context = new SourceControlContext(job.TenantId, job.ActorId);
					var store = scope.ServiceProvider.GetRequiredService<PersistentSourceControlStore>();
					var worker = "host-" + Guid.NewGuid().ToString("N");
					var fence = await store.TryClaimAsync(job.TenantId, job.Id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), stoppingToken);
					if (fence is null) continue;
					var actors = scope.ServiceProvider.GetRequiredService<ISourceControlActorResolver>();
					if (job.Kind == (int)SourceControlOperationKind.Commit)
						await scope.ServiceProvider.GetRequiredService<SourceControlClaimedCommitRunner>()
							.RunAsync(context, job.Id, worker, fence.Value, token => actors.ResolveAsync(context, token), stoppingToken);
					else
						await scope.ServiceProvider.GetRequiredService<SourceControlClaimedPushRunner>()
							.RunAsync(context, job.Id, worker, fence.Value, token => actors.ResolveAsync(context, token), stoppingToken);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
			catch (Exception) { logger.LogWarning("Source-control dispatch failed; durable operations remain available for recovery."); }
		} while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
