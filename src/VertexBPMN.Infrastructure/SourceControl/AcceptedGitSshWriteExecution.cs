using System.Text.Json.Serialization;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedGitSshWriteExecution(
	[property: JsonRequired] SourceControlExecutionTarget Target,
	[property: JsonRequired] ResolvedGitSshPolicy Policy)
{
	internal const int ContractVersion = 5;
	[JsonRequired]
	public int SchemaVersion { get; init; } = ContractVersion;
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? WriteRequestDigest { get; init; }
}
