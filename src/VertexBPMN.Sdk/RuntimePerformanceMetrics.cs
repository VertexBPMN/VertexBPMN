namespace VertexBPMN.Sdk;
public sealed record RuntimePerformanceMetrics(int TotalEvents, TimeSpan TotalExecutionTime, DateTime? FastestEventTime, DateTime? SlowestEventTime);
