namespace VertexBPMN.Domain.Model.Dmn;

/// <summary>
/// EF entity backing the DecisionInstance record.
/// </summary>
public class DecisionInstance
{
	public DecisionInstance(
		string id,
		string decisionDefinitionKey,
		string? tenantId,
		DateTime evaluationTime,
		IDictionary<string, object> inputVariables,
		IDictionary<string, object> outputVariables,
		bool failed = false,
		string? errorMessage = null)
	{
		Id = id;
		DecisionDefinitionKey = decisionDefinitionKey;
		TenantId = tenantId;
		EvaluationTime = evaluationTime;
		InputVariables = new Dictionary<string, object>(inputVariables, StringComparer.Ordinal);
		OutputVariables = new Dictionary<string, object>(outputVariables, StringComparer.Ordinal);
		Failed = failed;
		ErrorMessage = errorMessage;
	}
	public DecisionInstance() { }

	public string Id { get; set; } = default!;
	public string DecisionDefinitionKey { get; set; } = default!;
	public string? TenantId { get; set; }
	public DateTime EvaluationTime { get; set; }
	public Dictionary<string, object> InputVariables { get; set; } = new(StringComparer.Ordinal);
	public Dictionary<string, object> OutputVariables { get; set; } = new(StringComparer.Ordinal);
	public bool Failed { get; set; }
	public string? ErrorMessage { get; set; }
}
