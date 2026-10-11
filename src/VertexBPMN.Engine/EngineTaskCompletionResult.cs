namespace VertexBPMN.Engine;

public sealed record EngineTaskCompletionResult(EngineProcessInstance Instance, EngineTaskInstance Task, IReadOnlyList<EngineTaskInstance> NewlyActivatedTasks, bool ProcessCompleted);
