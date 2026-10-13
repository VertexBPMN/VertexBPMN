using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class SourceControlAuditHostedService(IServiceScopeFactory scopes, ILogger<SourceControlAuditHostedService> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
		do
		{
			try
			{
				await using var scope = scopes.CreateAsyncScope();
				await scope.ServiceProvider.GetRequiredService<SourceControlAuditExporter>().ExportAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
			catch (Exception) { logger.LogWarning("Source-control audit export failed; durable evidence remains pending for recovery."); }
		} while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
