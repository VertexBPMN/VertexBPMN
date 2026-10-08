using System.Net.Http.Json;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Studio.Services;

public sealed class HttpSourceControlService(IHttpClientFactory clients)
{
	private const string Prefix = "/api/source-control";
	private static string Escape(string value) => Uri.EscapeDataString(value);
	public Task<SourceControlAvailability> AvailabilityAsync(CancellationToken token) => GetAsync<SourceControlAvailability>(Prefix + "/availability", token);
	public Task<StudioGitRepository[]> RepositoriesAsync(CancellationToken token) => GetAsync<StudioGitRepository[]>(Prefix + "/repositories", token);
	public Task<StudioGitRepository> CreateAsync(StudioGitBindingRequest request, CancellationToken token) => PostAsync<StudioGitRepository>(Prefix + "/repositories", request, token);
	public Task<bool> GrantsAsync(Guid repositoryId, long revision, RepositoryGrant[] grants, CancellationToken token) =>
		PutAsync<bool>($"{Prefix}/repositories/{repositoryId}/grants", new { revision, grants }, token);
	public Task<SourceControlPage<string>> BranchesAsync(Guid id, string? cursor, CancellationToken token) =>
		GetAsync<SourceControlPage<string>>($"{Prefix}/repositories/{id}/branches?pageSize=100" + (cursor is null ? "" : "&cursor=" + Escape(cursor)), token);
	public Task<RevisionSelection> RevisionAsync(Guid id, string branch, CancellationToken token) =>
		GetAsync<RevisionSelection>($"{Prefix}/repositories/{id}/revision?branch={Escape(branch)}", token);
	public Task<SourceControlPage<RepositoryFile>> FilesAsync(Guid id, GitCommitId commit, string root, string? cursor, CancellationToken token) =>
		GetAsync<SourceControlPage<RepositoryFile>>($"{Prefix}/repositories/{id}/files?commit={Escape(commit.Value)}&root={Escape(root)}&pageSize=100"
			+ (cursor is null ? "" : "&cursor=" + Escape(cursor)), token);
	public Task<StudioGitSnapshot> FileAsync(Guid id, GitCommitId commit, string path, Guid generation, CancellationToken token) =>
		GetAsync<StudioGitSnapshot>($"{Prefix}/repositories/{id}/file?commit={Escape(commit.Value)}&path={Escape(path)}&generation={generation}", token);
	public Task<SourceControlPage<CommitSummary>> HistoryAsync(Guid id, GitCommitId commit, string path, string? cursor, CancellationToken token) =>
		GetAsync<SourceControlPage<CommitSummary>>($"{Prefix}/repositories/{id}/history?commit={Escape(commit.Value)}&path={Escape(path)}&pageSize=100"
			+ (cursor is null ? "" : "&cursor=" + Escape(cursor)), token);
	public Task<ModelDiff> DiffAsync(Guid id, GitCommitId commit, StudioGitSnapshot snapshot, CancellationToken token) =>
		PostAsync<ModelDiff>($"{Prefix}/repositories/{id}/diff", new { baseCommit = commit.Value, snapshot }, token);
	public Task<Guid> SessionAsync(Guid id, GitCommitId commit, Guid generation, CancellationToken token) =>
		PostAsync<Guid>($"{Prefix}/repositories/{id}/sessions", new { baseCommit = commit.Value, generation }, token);
	public Task<bool> SaveAsync(Guid sessionId, long revision, StudioGitSnapshot snapshot, CancellationToken token) =>
		PutAsync<bool>($"{Prefix}/sessions/{sessionId}/snapshots", new { revision, snapshots = new[] { snapshot } }, token);
	public Task<Guid> CommitAsync(Guid repositoryId, StudioGitCommitRequest request, CancellationToken token) =>
		PostAsync<Guid>($"{Prefix}/repositories/{repositoryId}/commits", request, token);
	public Task<Guid> PushAsync(Guid repositoryId, string key, Guid commitOperationId, GitCommitId? expected, CancellationToken token) =>
		PostAsync<Guid>($"{Prefix}/repositories/{repositoryId}/pushes", new { key, commitOperationId, expectedCommit = expected?.Value }, token);
	public Task<SourceControlOperation> OperationAsync(Guid id, CancellationToken token) => GetAsync<SourceControlOperation>($"{Prefix}/operations/{id}", token);
	public Task<Guid> PullRequestAsync(Guid repositoryId, StudioGitPullRequestRequest request, CancellationToken token) =>
		PostAsync<Guid>($"{Prefix}/repositories/{repositoryId}/pull-requests", request, token);
	public Task<PullRequestReceipt> PullRequestReceiptAsync(Guid id, CancellationToken token) =>
		GetAsync<PullRequestReceipt>($"{Prefix}/operations/{id}/pull-request-receipt", token);
	public Task<PullRequestReceipt> PullRequestStatusAsync(Guid id, CancellationToken token) =>
		GetAsync<PullRequestReceipt>($"{Prefix}/operations/{id}/pull-request-status", token);
	public Task<CommitReceipt> ReceiptAsync(Guid id, CancellationToken token) => GetAsync<CommitReceipt>($"{Prefix}/operations/{id}/commit-receipt", token);
	public Task<bool> AdvanceAsync(Guid sessionId, Guid pushOperationId, long revision, CancellationToken token) =>
		PostAsync<bool>($"{Prefix}/sessions/{sessionId}/advance", new { pushOperationId, revision }, token);

	private async Task<T> GetAsync<T>(string path, CancellationToken token)
	{
		using var response = await clients.CreateClient("VertexBPMN.Api").GetAsync(path, token);
		return await ReadAsync<T>(response, token);
	}
	private async Task<T> PostAsync<T>(string path, object request, CancellationToken token)
	{
		using var response = await clients.CreateClient("VertexBPMN.Api").PostAsJsonAsync(path, request, token);
		return await ReadAsync<T>(response, token);
	}
	private async Task<T> PutAsync<T>(string path, object request, CancellationToken token)
	{
		using var response = await clients.CreateClient("VertexBPMN.Api").PutAsJsonAsync(path, request, token);
		return await ReadAsync<T>(response, token);
	}
	private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken token)
	{
		await ApiResponseErrors.EnsureSuccessAsync(response, token);
		return await response.Content.ReadFromJsonAsync<T>(cancellationToken: token)
			?? throw new HttpRequestException("The API returned no source-control result.");
	}
}

public sealed record StudioGitRepository(RepositoryBinding Binding, long Revision, RepositoryGrant[] Grants);
public sealed record StudioGitBindingRequest(string Remote, string? CredentialReference, string DefaultBranch, string ReleaseBranch, string[] ModelRoots);
public sealed record StudioGitSnapshot(string Path, Guid Generation, long LocalRevision, byte[] Bytes, string ContentSha256);
public sealed record StudioGitCommitRequest(string Key, Guid SessionId, long SessionRevision, string BaseCommit, string Message, StudioGitSnapshot[] Snapshots);
public sealed record StudioGitPullRequestRequest(string Key, Guid PushOperationId, string BaseBranch, string Title, string Description);
