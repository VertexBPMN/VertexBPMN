namespace VertexBPMN.Engine;

public sealed record EngineProcessInstance(
	Guid Id,
	string ProcessKey,
	DateTime StartedAt,
	Dictionary<string, object?> Variables,
	List<EngineToken> Tokens,
	List<EngineTaskInstance> ActiveTasks,
	List<EngineHistoryEvent> History,
	bool Completed,
	DateTime? EndedAt
)
{
	public bool Completed { get; set; } = Completed;
	public DateTime? EndedAt { get; set; } = EndedAt;
	public IReadOnlyList<EngineTaskInstance> GetOpenTasks() => [.. ActiveTasks.Where(t => !t.Completed)];
}
