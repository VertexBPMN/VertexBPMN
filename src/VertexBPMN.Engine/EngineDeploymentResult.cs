namespace VertexBPMN.Engine;

public sealed record EngineDeploymentResult(
	EngineProcessDefinition? ProcessDefinition,
	IReadOnlyList<string> MappingDiagnostics
);
