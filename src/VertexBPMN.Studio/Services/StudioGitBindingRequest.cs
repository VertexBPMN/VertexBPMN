using System.Net.Http.Json;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Studio.Services;
internal sealed record StudioGitBindingRequest(string Remote, string? CredentialReference, string DefaultBranch, string ReleaseBranch, string[] ModelRoots)
{
	public Guid? ConnectionProfileId { get; init; }
	public long? ProfileRevision { get; init; }
}
