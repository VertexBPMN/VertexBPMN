using System.Net.Http.Json;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Studio.Services;

internal sealed record StudioGitRepository(RepositoryBinding Binding, long Revision, RepositoryGrant[] Grants);
