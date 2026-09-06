using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Operational;

/// <summary>
/// Entfernt verfallene OAuth2-Flow-Zustände periodisch, damit nie-transaktionale,
/// abgebrochene Autorisationen keinen Storage-Müll hinterlassen.
/// </summary>
public sealed class OAuth2FlowStateCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<OAuth2FlowStateCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
                var removed = await db.OAuth2FlowStates
                    .Where(s => s.ExpiresAt <= DateTime.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);
                if (removed > 0)
                    logger.LogInformation("Removed {Count} expired OAuth2 flow states", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clean expired OAuth2 flow states");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
