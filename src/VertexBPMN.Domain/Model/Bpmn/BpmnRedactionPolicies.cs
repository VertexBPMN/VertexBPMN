namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Configuration for BPMN content redaction policies.
/// </summary>
public sealed record BpmnRedactionPolicies
{
	/// <summary>
	/// Enable confidential data stripping.
	/// </summary>
	public bool StripConfidentialData { get; init; }

	/// <summary>
	/// Namespaces to redact completely.
	/// </summary>
	public HashSet<string> RedactedNamespaces { get; init; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Attributes to redact (partial name matching).
	/// </summary>
	public HashSet<string> RedactedAttributes { get; init; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Elements to redact completely.
	/// </summary>
	public HashSet<string> RedactedElements { get; init; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Attributes to explicitly preserve (override redaction).
	/// </summary>
	public HashSet<string> PreserveAttributes { get; init; } = new(StringComparer.Ordinal);
}
