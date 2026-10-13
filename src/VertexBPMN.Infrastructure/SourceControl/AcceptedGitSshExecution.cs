using System.Text.Json.Serialization;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Durable SSH context without DNS addresses, private keys or credential leases.</summary>
internal sealed record AcceptedGitSshExecution(
	[property: JsonRequired] SourceControlExecutionTarget Target,
	[property: JsonRequired] ResolvedGitSshPolicy Policy)
{
	internal const int ContractVersion = 4;

	[JsonRequired]
	public int SchemaVersion { get; init; } = ContractVersion;
}
