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

    public async ValueTask SuspendProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var runtimeService = _serviceProvider.GetRequiredService<IRuntimeService>();
        var instance = await runtimeService.GetByIdAsync(processInstanceId, cancellationToken);
        EnsureTenantAccess(instance, tenantId);
        if (instance is not null)
            await runtimeService.SuspendAsync(processInstanceId, cancellationToken);
    }

    public async ValueTask ResumeProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var runtimeService = _serviceProvider.GetRequiredService<IRuntimeService>();
        var instance = await runtimeService.GetByIdAsync(processInstanceId, cancellationToken);
        EnsureTenantAccess(instance, tenantId);
        if (instance is not null)
            await runtimeService.ResumeAsync(processInstanceId, cancellationToken);
    }

    public async ValueTask DeleteProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var runtimeService = _serviceProvider.GetRequiredService<IRuntimeService>();
        var instance = await runtimeService.GetByIdAsync(processInstanceId, cancellationToken);
        EnsureTenantAccess(instance, tenantId);
        if (instance is not null)
            await runtimeService.DeleteAsync(processInstanceId, cancellationToken);
    }

    public ValueTask ExecuteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    private static readonly DateTime _startTime = DateTime.UtcNow;
    private static void EnsureTenantAccess(ProcessInstance? instance, string? tenantId)
    {
        if (instance is not null
            && tenantId is not null
            && !string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The process instance belongs to another tenant.");
        }
    }

    public async ValueTask<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await _metricsReader.ReadAsync(cancellationToken);
        var metrics = persisted.ToDictionary(entry => entry.Key, entry => (object)entry.Value);
        metrics["engine_uptime_seconds"] = (long)(DateTime.UtcNow - _startTime).TotalSeconds;
        return metrics;
    }
}
