using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.JobExecutor;

/// <summary>
/// Background service that polls and executes due jobs (timer, async, etc.).
/// </summary>

public class JobExecutorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutorService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public JobExecutorService(IServiceProvider serviceProvider, ILogger<JobExecutorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                    var eventSink = scope.ServiceProvider.GetService<IProcessMiningEventSink>();
                    await foreach (var job in jobRepo.ListDueAsync(DateTime.UtcNow, stoppingToken))
                    {
                        try
                        {
                            // TODO: Dispatch to job handler based on job.Type
                            _logger.LogInformation($"Executing job {job.Id} of type {job.Type}");
                            // Simulate job execution
                            await System.Threading.Tasks.Task.Delay(100, stoppingToken);
                            await jobRepo.DeleteAsync(job.Id, stoppingToken);
                            if (eventSink != null)
                            {
                                await eventSink.EmitAsync(new ProcessMiningEvent {
                                    EventType = "JobExecuted",
                                    ProcessInstanceId = job.ProcessInstanceId.ToString(),
                                    TenantId = job.TenantId,
                                    Timestamp = DateTimeOffset.UtcNow,
                                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object> { { "JobId", job.Id.ToString() }, { "Type", job.Type } })
                                }, stoppingToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Job {job.Id} failed");
                            if (eventSink != null)
                            {
                                await eventSink.EmitAsync(new ProcessMiningEvent {
                                    EventType = "JobFailed",
                                    ProcessInstanceId = job.ProcessInstanceId.ToString(),
                                    TenantId = job.TenantId,
                                    Timestamp = DateTimeOffset.UtcNow,
                                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object> { { "JobId", job.Id.ToString() }, { "Type", job.Type }, { "ErrorMessage", ex.Message } })
                                }, stoppingToken);
                            }
                            // TODO: Retry/Backoff logic, update job.Retries, ErrorMessage
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job polling failed");
            }
            await System.Threading.Tasks.Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
