namespace VertexBPMN.Sdk;
public sealed record RuntimeExecutionTrace(Guid SessionId, Guid ProcessInstanceId, DateTime StartedAt, DateTime? EndedAt, IReadOnlyList<RuntimeTraceEvent> Events, RuntimePerformanceMetrics? PerformanceMetrics);
