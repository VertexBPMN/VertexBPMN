namespace VertexBPMN.Engine;

public sealed record EngineSequenceFlow(
	string Id,
	string SourceId,
	string TargetId,
	bool IsDefault,
	string? ConditionExpression,
	int? Priority
);
