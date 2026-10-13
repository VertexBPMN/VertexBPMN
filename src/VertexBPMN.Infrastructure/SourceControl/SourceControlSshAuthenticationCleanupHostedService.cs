using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Remove only expired owned authentication files, including after source control is disabled.</summary>
internal sealed class SourceControlSshAuthenticationCleanupHostedService(IServiceScopeFactory scopes,
	ILogger<SourceControlSshAuthenticationCleanupHostedService> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
		do
		{
			try
			{
				await using var scope = scopes.CreateAsyncScope();
				_ = await scope.ServiceProvider.GetRequiredService<GitSshAuthenticationFileRecovery>()
					.PruneAsync(DateTimeOffset.UtcNow, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception)
			{
				logger.LogWarning("SSH authentication-file maintenance failed; owned expired files will be retried.");
			}
		} while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
