namespace VertexBPMN.Engine;

public sealed record EngineFlowNode(
	string Id,
	string Type,
	string? SubprocessId,
	bool IsGateway,
	bool IsEvent,
	bool IsTask,
	bool IsSubprocess,
	bool IsEndEvent,
	bool IsUserTask
);
