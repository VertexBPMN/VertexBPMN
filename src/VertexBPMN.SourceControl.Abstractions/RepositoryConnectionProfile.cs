using System.Text.Json.Serialization;

namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Immutable, tenant-bound connection configuration. Capabilities describe the profile,
/// not user permissions or proof of adapter/server availability. Null Hosting means Git only.
/// </summary>
public sealed record RepositoryConnectionProfile(Guid Id, string TenantId, long Revision,
	GitTransportKind Transport, GitAuthenticationKind Authentication, RepositoryEndpoint GitEndpoint,
	string? GitCredentialReference, SourceControlCapability GitCapabilities)
{
	public const int CurrentSchemaVersion = 1;

	[JsonRequired]
	public int SchemaVersion { get; init; } = CurrentSchemaVersion;
	public RepositoryHostingConnection? Hosting { get; init; }
}
