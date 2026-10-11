using System.Net.Http.Json;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Studio.Services;
internal sealed record StudioGitCommitRequest(string Key, Guid SessionId, long SessionRevision, string BaseCommit, string Message, StudioGitSnapshot[] Snapshots);
