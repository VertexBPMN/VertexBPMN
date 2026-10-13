using System.Text.Json.Serialization;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Durable, secret-free context. DNS addresses and credential leases are resolved again at execution.</summary>
internal sealed record AcceptedGitHttpsExecution(
	[property: JsonRequired] SourceControlExecutionTarget Target,
	[property: JsonRequired] ResolvedGitHttpsPolicy Policy)
{
	internal const int ContractVersion = 2;

	[JsonRequired]
	public int SchemaVersion { get; init; } = ContractVersion;

	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? WriteRequestDigest { get; init; }
}
