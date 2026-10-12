using System.Text.Json.Serialization;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedHostingExecution(
	[property: JsonRequired] SourceControlExecutionTarget Target,
	[property: JsonRequired] RepositoryHostingTarget HostingTarget,
	[property: JsonRequired] ResolvedGitHttpsPolicy ApiPolicy,
	string? RequestDigest = null)
{
	internal const int ContractVersion = 3;
	[JsonRequired]
	public int SchemaVersion { get; init; } = ContractVersion;
}
