using System.Text.Json.Serialization;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>A fixed object ID accepted for private fetch; never an execution-time revision expression.</summary>
internal sealed record AcceptedGitRevisionRead([property: JsonRequired] GitCommitId? Revision)
{
	[JsonRequired]
	public int SchemaVersion { get; init; } = 1;
	public AcceptedGitReadQuery? Query { get; init; }
}
