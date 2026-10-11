namespace VertexBPMN.Engine;

public sealed record EngineStartResult(EngineProcessInstance Instance, IReadOnlyList<EngineTaskInstance> ActivatedTasks);
