using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Operational;

/// <summary>
/// Bounded recovery pump. Each pass uses a fresh scope so database state is not retained
/// between polls and competing replicas converge through the services' CAS transitions.
/// </summary>
public sealed class ExternalTaskRecoveryHostedService(
    IServiceScopeFactory scopes,
    ILogger<ExternalTaskRecoveryHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consecutiveFailures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var recovery = scope.ServiceProvider.GetRequiredService<IExternalTaskRecoveryService>();
                    var runtime = scope.ServiceProvider.GetRequiredService<IProcessExecutionRuntime>();
                    var recovered = await recovery.RecoverAsync(100, stoppingToken);
                    var continued = await runtime.ProcessExternalTaskContinuationsAsync(100, stoppingToken);
                    if (recovered.Conflicts + continued.Conflicts == 0) break;
                    if (attempt < 2) await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                }
                consecutiveFailures = 0;
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                consecutiveFailures++;
                var seconds = Math.Min(60, 1 << Math.Min(6, consecutiveFailures - 1));
                logger.LogError(exception,
                    "External-task recovery pass failed; retrying after {DelaySeconds} seconds.", seconds);
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
        }
    }
}
