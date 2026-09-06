using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// Background service that periodically polls due <see cref="Domain.Entities.PollingTriggerRecord"/> entries.
/// Each due trigger is leased (so concurrent API replicas never double-poll), executed through the configured
/// connector, and a new process instance is started when new data is detected.
/// </summary>
public sealed class PollingSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<PollingSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PollingSchedulerService started");
        try
        {
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunIterationAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "PollingSchedulerService error");
        }
    }

    internal async Task RunIterationAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPollingTriggerRepository>();
        var poller = scope.ServiceProvider.GetRequiredService<PollingTriggerPoller>();

        foreach (var trigger in await repository.ListDueAsync(DateTime.UtcNow, stoppingToken))
        {
            var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";
            if (!await repository.TryLeaseAsync(trigger, workerId, DateTime.UtcNow.AddMinutes(2), stoppingToken))
            {
                logger.LogDebug("Polling trigger {TriggerId} was leased by another worker", trigger.Id);
                continue;
            }

            var outcome = await poller.PollAsync(trigger, stoppingToken);
            PollingTriggerPoller.ApplyOutcome(trigger, outcome, DateTime.UtcNow);
            await repository.UpdateAsync(trigger, stoppingToken);

            if (outcome.Status == PollTriggerStatus.Started)
                logger.LogInformation("Polling trigger {TriggerId} started instance {InstanceId}", trigger.Id, outcome.InstanceId);
        }
    }
}
