using System.Text.Json.Serialization;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ConnectorTemplateWriteRequest(
	string Name,
	string Category,
	IReadOnlyList<string> AppliesTo,
	string Runtime,
	string? Icon,
	IReadOnlyList<ConnectorTemplateProperty> Properties);
