using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Engine.Execution;

/// <summary>
/// Database-backed worker registry. Heartbeats remain visible to every API and
/// worker replica and optimistic concurrency prevents lost status updates.
/// </summary>
public sealed class PersistentWorkerNodeManager(
    BpmnDbContext db,
    ILogger<PersistentWorkerNodeManager> logger) : IWorkerNodeManager
{
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(2);

    public async Task<WorkerNode> RegisterWorkerAsync(WorkerRegistrationRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HostName);
        if (request.MaxCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Worker capacity must be greater than zero.");

        var now = DateTime.UtcNow;
        var worker = await db.WorkerRegistrations.SingleOrDefaultAsync(x => x.Id == request.WorkerId);
        if (worker is null)
        {
            worker = new WorkerRegistration
            {
                Id = request.WorkerId,
                RegisteredAt = now
            };
            db.WorkerRegistrations.Add(worker);
        }

        worker.HostName = request.HostName;
        worker.Port = request.Port;
        worker.SupportedNodeTypes = request.SupportedNodeTypes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        worker.MaxCapacity = request.MaxCapacity;
        worker.LastHeartbeat = now;
        worker.Revision++;
        await db.SaveChangesAsync();

        logger.LogInformation("Worker {WorkerId} registered at {Host}:{Port}", worker.Id, worker.HostName, worker.Port);
        return ToWorkerNode(worker);
    }

    public async System.Threading.Tasks.Task UnregisterWorkerAsync(string workerId)
    {
        var worker = await db.WorkerRegistrations.SingleOrDefaultAsync(x => x.Id == workerId);
        if (worker is null)
            return;

        db.WorkerRegistrations.Remove(worker);
        await db.SaveChangesAsync();
        logger.LogInformation("Worker {WorkerId} unregistered", workerId);
    }

    public async Task<List<WorkerNode>> GetActiveWorkersAsync()
    {
        var cutoff = DateTime.UtcNow - HeartbeatTimeout;
        return (await db.WorkerRegistrations.AsNoTracking()
                .Where(x => x.LastHeartbeat >= cutoff)
                .OrderBy(x => x.CurrentLoad)
                .ToListAsync())
            .Select(ToWorkerNode)
            .ToList();
    }

    public async Task<WorkerNode?> GetWorkerAsync(string workerId)
    {
        var worker = await db.WorkerRegistrations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workerId)
            ?? throw new KeyNotFoundException($"Worker '{workerId}' is not registered.");
        return ToWorkerNode(worker);
    }

    public async System.Threading.Tasks.Task UpdateWorkerStatusAsync(string workerId, WorkerStatus status)
    {
        var worker = await db.WorkerRegistrations.SingleOrDefaultAsync(x => x.Id == workerId)
            ?? throw new KeyNotFoundException($"Worker '{workerId}' is not registered.");

        worker.CurrentLoad = status.CurrentLoad;
        worker.CpuUsage = status.CpuUsage;
        worker.MemoryUsage = status.MemoryUsage;
        worker.ActiveTasks = status.ActiveTasks;
        worker.LastHeartbeat = status.Timestamp == default ? DateTime.UtcNow : status.Timestamp.ToUniversalTime();
        worker.Revision++;
        await db.SaveChangesAsync();
    }

    public async Task<WorkerCapacityInfo> GetWorkerCapacityAsync(string workerId)
    {
        var worker = await db.WorkerRegistrations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workerId);
        if (worker is null)
            return new WorkerCapacityInfo(workerId, false, 0, 0, 0);

        var available = Math.Max(0, worker.MaxCapacity - worker.CurrentLoad);
        var utilization = worker.MaxCapacity == 0 ? 0 : worker.CurrentLoad * 100d / worker.MaxCapacity;
        return new WorkerCapacityInfo(worker.Id, worker.LastHeartbeat >= DateTime.UtcNow - HeartbeatTimeout,
            worker.CurrentLoad, worker.MaxCapacity, available, Math.Round(utilization, 1));
    }

    public async Task<bool> IsWorkerHealthyAsync(string workerId)
    {
        var cutoff = DateTime.UtcNow - HeartbeatTimeout;
        return await db.WorkerRegistrations.AsNoTracking().AnyAsync(x => x.Id == workerId && x.LastHeartbeat >= cutoff);
    }

    public async Task<List<WorkerNode>> GetWorkersForNodeTypeAsync(string nodeType)
    {
        var workers = await GetActiveWorkersAsync();
        return workers.Where(x => x.SupportedNodeTypes.Contains(nodeType, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public async System.Threading.Tasks.Task NotifyWorkersAsync(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        db.RuntimeOutbox.Add(new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "WorkerNotification",
            Payload = System.Text.Json.JsonSerializer.Serialize(new { message }),
            State = "Pending",
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<WorkerPerformanceMetrics> GetWorkerPerformanceAsync(string workerId)
    {
        var worker = await db.WorkerRegistrations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workerId)
            ?? throw new KeyNotFoundException($"Worker '{workerId}' is not registered.");
        return new WorkerPerformanceMetrics
        {
            WorkerId = worker.Id,
            RegisteredAt = worker.RegisteredAt,
            TotalTasksProcessed = checked((int)Math.Min(int.MaxValue, worker.TotalTasksProcessed)),
            AverageProcessingTime = worker.TotalTasksProcessed == 0
                ? TimeSpan.Zero
                : TimeSpan.FromMilliseconds(worker.TotalProcessingMilliseconds / worker.TotalTasksProcessed),
            LastActivity = worker.LastHeartbeat,
            CpuUsage = worker.CpuUsage,
            MemoryUsage = worker.MemoryUsage,
            ActiveTasks = worker.ActiveTasks
        };
    }

    private static WorkerNode ToWorkerNode(WorkerRegistration worker) => new(
        worker.Id,
        worker.HostName,
        worker.Port,
        worker.LastHeartbeat,
        worker.SupportedNodeTypes,
        worker.CurrentLoad,
        worker.MaxCapacity,
        SupportsDmn: worker.SupportedNodeTypes.Contains("dmn", StringComparer.OrdinalIgnoreCase),
        SupportsCmmn: worker.SupportedNodeTypes.Contains("cmmn", StringComparer.OrdinalIgnoreCase),
        SupportsBpmn: worker.SupportedNodeTypes.Contains("bpmn", StringComparer.OrdinalIgnoreCase));
}
