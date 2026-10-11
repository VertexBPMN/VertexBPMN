namespace VertexBPMN.Engine;

public sealed record EngineTaskInstance(Guid Id, string NodeId, string Name, DateTime CreatedAt, Dictionary<string, object?> Variables)
{
	public bool Completed { get; set; }
	public DateTime? CompletedAt { get; set; }
}
