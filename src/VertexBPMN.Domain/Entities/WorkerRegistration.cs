namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Durable worker registration and heartbeat state.
/// </summary>
public sealed class WorkerRegistration
{
    public string Id { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; }
    public List<string> SupportedNodeTypes { get; set; } = [];
    public int CurrentLoad { get; set; }
    public int MaxCapacity { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public long TotalTasksProcessed { get; set; }
    public double TotalProcessingMilliseconds { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public int ActiveTasks { get; set; }
    public long Revision { get; set; }
}
