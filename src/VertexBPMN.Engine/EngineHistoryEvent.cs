namespace VertexBPMN.Engine;

public sealed record EngineHistoryEvent(long Sequence, string EventType, string NodeId, DateTime Timestamp, string? Details = null);
