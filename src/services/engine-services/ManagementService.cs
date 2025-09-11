using System;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Core.Services;

/// <summary>
/// In-memory implementation of IManagementService for development and testing.
/// </summary>
public class ManagementService : IManagementService
{
    private readonly ConcurrentDictionary<Guid, bool> _suspended = new();
    private readonly IServiceProvider _serviceProvider;

    public ManagementService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ValueTask SuspendProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
    {
        _suspended[processInstanceId] = true;
        var eventSink = _serviceProvider.GetRequiredService<IProcessMiningEventSink>();
        eventSink.EmitAsync(new ProcessMiningEvent {
            EventType = "ProcessSuspended",
            ProcessInstanceId = processInstanceId.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
    {
        _suspended[processInstanceId] = false;
        var eventSink = _serviceProvider.GetRequiredService<IProcessMiningEventSink>();
        eventSink.EmitAsync(new ProcessMiningEvent {
            EventType = "ProcessResumed",
            ProcessInstanceId = processInstanceId.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
    {
        _suspended.TryRemove(processInstanceId, out _);
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
    private static int _processInstanceCount = 42; // TODO: Query real repo
    private static int _jobCount = 7; // TODO: Query real repo

    public ValueTask<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
        {
            ["process_instances_total"] = _processInstanceCount,
            ["jobs_total"] = _jobCount,
            ["engine_uptime_seconds"] = (int)(DateTime.UtcNow - _startTime).TotalSeconds
        });
}
