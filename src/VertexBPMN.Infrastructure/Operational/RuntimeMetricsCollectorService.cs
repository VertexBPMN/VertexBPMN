using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Operational;

public sealed class RuntimeMetricsCollectorService(
    IServiceScopeFactory scopeFactory,
    RuntimeMetricsState state,
    ILogger<RuntimeMetricsCollectorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reader = scope.ServiceProvider.GetRequiredService<IRuntimeMetricsReader>();
                state.Update(await reader.ReadAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh durable runtime metrics");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
