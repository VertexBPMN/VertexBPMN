namespace VertexBPMN.Sdk;
public sealed record RuntimeTraceEvent(string Type, string ActivityId, DateTime Timestamp, string? Details, TimeSpan? Duration);
