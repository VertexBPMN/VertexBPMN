using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Runs bounded maintenance after startup and periodically; never repeats an external write.</summary>
internal sealed class SourceControlMaintenanceHostedService(
    IServiceScopeFactory scopes, IOptions<SourceControlOptions> options,
    ILogger<SourceControlMaintenanceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<PersistentSourceControlStore>();
                var workspace = scope.ServiceProvider.GetRequiredService<SourceControlWorkspace>();
                var now = DateTimeOffset.UtcNow;
                await store.DetectExpiredLeasesAsync(now, stoppingToken);
                await workspace.PruneCompletedAsync(now, stoppingToken);
                await store.PruneExpiredDetailsAsync(now, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // No exception object: provider errors can embed connection strings or paths.
                logger.LogWarning("Source-control maintenance failed; retained data will be retried on the next tick.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
