namespace VertexBPMN.Core.Engine;

/// <summary>
/// Worker performance metrics
/// </summary>
public record WorkerPerformanceMetrics
{
    public string WorkerId { get; init; } = string.Empty;
    public DateTime RegisteredAt { get; init; }
    public int TotalTasksProcessed { get; init; }
    public TimeSpan AverageProcessingTime { get; init; }
    public DateTime LastActivity { get; init; }
    public double CpuUsage { get; init; }
    public double MemoryUsage { get; init; }
    public int ActiveTasks { get; init; }
    public TimeSpan Uptime => DateTime.UtcNow - RegisteredAt;
    public double TasksPerMinute => TotalTasksProcessed > 0 && Uptime.TotalMinutes > 0 
        ? TotalTasksProcessed / Uptime.TotalMinutes 
        : 0;
}