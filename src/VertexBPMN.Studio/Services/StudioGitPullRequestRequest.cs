using System.Net.Http.Json;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Studio.Services;
internal sealed record StudioGitPullRequestRequest(string Key, Guid PushOperationId, string BaseBranch, string Title, string Description);
