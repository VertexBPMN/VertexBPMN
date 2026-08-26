using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// Management operations backed by the runtime services and durable metrics reader.
/// </summary>
public class ManagementService : IManagementService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRuntimeMetricsReader _metricsReader;

    public ManagementService(IServiceProvider serviceProvider, IRuntimeMetricsReader metricsReader)
    {
        _serviceProvider = serviceProvider;
        _metricsReader = metricsReader;
    }

    public ValueTask SuspendProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var eventSink = _serviceProvider.GetRequiredService<IProcessMiningEventSink>();
        eventSink.EmitAsync(new ProcessMiningEvent {
            EventType = "ProcessSuspended",
            ProcessInstanceId = processInstanceId.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var eventSink = _serviceProvider.GetRequiredService<IProcessMiningEventSink>();
        eventSink.EmitAsync(new ProcessMiningEvent {
            EventType = "ProcessResumed",
            ProcessInstanceId = processInstanceId.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var eventSink = _serviceProvider.GetRequiredService<IProcessMiningEventSink>();
        eventSink.EmitAsync(new ProcessMiningEvent {
            EventType = "ProcessDeleted",
            ProcessInstanceId = processInstanceId.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask ExecuteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    private static readonly DateTime _startTime = DateTime.UtcNow;
    public async ValueTask<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await _metricsReader.ReadAsync(cancellationToken);
        var metrics = persisted.ToDictionary(entry => entry.Key, entry => (object)entry.Value);
        metrics["engine_uptime_seconds"] = (long)(DateTime.UtcNow - _startTime).TotalSeconds;
        return metrics;
    }
}
