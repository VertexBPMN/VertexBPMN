using System.Net.Http.Json;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Studio.Services;
internal sealed record StudioGitSnapshot(string Path, Guid Generation, long LocalRevision, byte[] Bytes, string ContentSha256);
