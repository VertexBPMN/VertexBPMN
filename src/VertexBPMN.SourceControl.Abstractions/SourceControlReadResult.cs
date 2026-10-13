using System.Text.Json;
using System.Text.Json.Serialization;

namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Durable read payload. Kind is selected by the accepted request, never a caller-selected CLR type.</summary>
public sealed record SourceControlReadResult([property: JsonRequired] Guid OperationId, [property: JsonRequired] string Kind,
	[property: JsonRequired] JsonElement Value)
{
	[JsonRequired]
	public int SchemaVersion { get; init; } = 1;
}
