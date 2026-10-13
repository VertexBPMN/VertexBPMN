using System.Text.Json;
using System.Text.Json.Serialization;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ResolvedGitSshPolicy(
	[property: JsonRequired] GitSshNetworkPolicy Network,
	[property: JsonRequired] GitSshTrustProfile Trust)
{
	internal bool Matches(ResolvedGitSshPolicy current) =>
		JsonSerializer.Serialize(this) == JsonSerializer.Serialize(current);
}
