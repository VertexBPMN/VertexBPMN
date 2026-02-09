namespace VertexBPMN.Domain.Entities;

public class SystemMetrics
{
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public double UptimeSeconds { get; set; }
    public long GCMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public float? CpuUsagePercent { get; set; }
    public float? AvailableMemoryMB { get; set; }
}