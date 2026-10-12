using System.Text.Json.Serialization;

namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Secret-free target frozen at job acceptance. Persist alongside the existing actor,
/// tenant and content identity, never resolve it from a changed profile during recovery.
/// </summary>
public sealed record SourceControlExecutionTarget(Guid RepositoryId, long BindingRevision,
	Uri Remote, RepositoryConnectionProfile Profile, SourceControlCapability RequiredCapabilities)
{
	public const int CurrentSchemaVersion = 1;

	[JsonRequired]
	public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
