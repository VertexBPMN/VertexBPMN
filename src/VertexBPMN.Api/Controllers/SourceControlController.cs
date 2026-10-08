using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/source-control")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequestSizeLimit(3 * 1024 * 1024)]
public sealed class SourceControlController(PersistentSourceControlStore store, IModelSourceControlProvider provider,
	ISourceControlActorResolver actors, SourceControlPullRequestStatusReader pullRequests) : ControllerBase
{
	[HttpGet("availability")]
	public Task<ActionResult<SourceControlAvailability>> Availability(CancellationToken cancellationToken) =>
		RunAsync(async () => await provider.GetAvailabilityAsync(cancellationToken));

	[HttpGet("repositories")]
	public Task<ActionResult<IReadOnlyList<RepositoryAccessSnapshot>>> Repositories(CancellationToken cancellationToken) =>
		RunAsync(async () => await store.ListBindingsAsync(Context(), await RolesAsync(cancellationToken), cancellationToken));

	[HttpPost("repositories")]
	public Task<ActionResult<RepositoryAccessSnapshot>> CreateRepository(BindingRequest request, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.CreateBindingAsync(Context(), await RolesAsync(cancellationToken),
			new(Guid.NewGuid(), Context().TenantId, new(request.Remote, UriKind.Absolute), request.CredentialReference,
				request.DefaultBranch, request.ReleaseBranch, request.ModelRoots), cancellationToken));

	[HttpPut("repositories/{id:guid}/grants")]
	public Task<ActionResult<bool>> Grants(Guid id, GrantsRequest request, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.ReplaceGrantsAsync(Context(), id, await RolesAsync(cancellationToken), request.Revision, request.Grants, cancellationToken));

	[HttpGet("repositories/{id:guid}/branches")]
	public Task<ActionResult<SourceControlPage<string>>> Branches(Guid id, int pageSize = 100, string? cursor = null, CancellationToken cancellationToken = default) =>
		RunAsync(async () => await provider.ListBranchesAsync(Context(), await BindingAsync(id, cancellationToken), pageSize, cursor, cancellationToken));

	[HttpGet("repositories/{id:guid}/revision")]
	public Task<ActionResult<RevisionSelection>> Revision(Guid id, string branch, CancellationToken cancellationToken) =>
		RunAsync(async () => await provider.ResolveBranchAsync(Context(), await BindingAsync(id, cancellationToken), branch, cancellationToken));

	[HttpGet("repositories/{id:guid}/files")]
	public Task<ActionResult<SourceControlPage<RepositoryFile>>> Files(Guid id, string commit, string root, int pageSize = 100,
		string? cursor = null, CancellationToken cancellationToken = default) => RunAsync(async () =>
			await provider.ListFilesAsync(Context(), await BindingAsync(id, cancellationToken), new(new(commit), root, pageSize, cursor), cancellationToken));

	[HttpGet("repositories/{id:guid}/file")]
	public Task<ActionResult<SnapshotResponse>> File(Guid id, string commit, string path, Guid generation, CancellationToken cancellationToken) =>
		RunAsync(async () => SnapshotResponse.From(await provider.ReadFileAsync(Context(), await BindingAsync(id, cancellationToken),
			new(new(commit), path), generation, cancellationToken)));

	[HttpGet("repositories/{id:guid}/history")]
	public Task<ActionResult<SourceControlPage<CommitSummary>>> History(Guid id, string commit, string path, int pageSize = 100,
		string? cursor = null, CancellationToken cancellationToken = default) => RunAsync(async () =>
			await provider.ReadHistoryAsync(Context(), await BindingAsync(id, cancellationToken), new(path, new(commit), pageSize, cursor), cancellationToken));

	[HttpPost("repositories/{id:guid}/diff")]
	public Task<ActionResult<ModelDiff>> Diff(Guid id, DiffBody request, CancellationToken cancellationToken) =>
		RunAsync(async () => await provider.CompareAsync(Context(), await BindingAsync(id, cancellationToken), new(new(request.BaseCommit), request.Snapshot.ToSnapshot()), cancellationToken));

	[HttpPost("repositories/{id:guid}/sessions")]
	public Task<ActionResult<Guid>> Session(Guid id, SessionRequest request, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.CreateSessionAsync(Context(), id, await RolesAsync(cancellationToken), new(request.BaseCommit), request.Generation, cancellationToken));

	[HttpPut("sessions/{id:guid}/snapshots")]
	public Task<ActionResult<bool>> Snapshots(Guid id, SnapshotsRequest request, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.SaveSnapshotsAsync(Context(), id, await RolesAsync(cancellationToken), request.Revision,
			request.Snapshots.Select(x => x.ToSnapshot()).ToArray(), cancellationToken));

	[HttpPost("sessions/{id:guid}/advance")]
	public Task<ActionResult<bool>> Advance(Guid id, AdvanceRequest request, CancellationToken cancellationToken) =>
		RunAsync(async () => await store.AdvanceSessionAfterPushAsync(Context(), id, request.PushOperationId, request.Revision,
			await RolesAsync(cancellationToken), cancellationToken));

	[HttpPost("repositories/{id:guid}/commits")]
	public async Task<ActionResult<Guid>> Commit(Guid id, CommitRequest request, CancellationToken cancellationToken)
	{
		var result = await RunAsync(async () => await store.EnqueueCommitAsync(Context(), id, await RolesAsync(cancellationToken), new(request.Key),
			new(request.SessionId, request.SessionRevision, new(request.BaseCommit), SourceControlInputPolicy.WorkBranch(request.SessionId), request.Message,
				request.Snapshots.Select(x => x.ToSnapshot()).ToArray()), cancellationToken));
		return result.Result is null ? Accepted($"/api/source-control/operations/{result.Value}", result.Value) : result;
	}

	[HttpPost("repositories/{id:guid}/pushes")]
	public async Task<ActionResult<Guid>> Push(Guid id, PushRequest request, CancellationToken cancellationToken)
	{
		var result = await RunAsync(async () => await store.EnqueuePushAsync(Context(), id, await RolesAsync(cancellationToken), new(request.Key),
			new(request.CommitOperationId, request.ExpectedCommit is null ? ExpectedRemoteRef.Absent : ExpectedRemoteRef.At(new(request.ExpectedCommit))), cancellationToken));
		return result.Result is null ? Accepted($"/api/source-control/operations/{result.Value}", result.Value) : result;
	}

	[HttpPost("repositories/{id:guid}/pull-requests")]
	public async Task<ActionResult<Guid>> PullRequest(Guid id, PullRequestRequest request, CancellationToken cancellationToken)
	{
		var result = await RunAsync(async () => await store.EnqueuePullRequestAsync(Context(), id,
			await RolesAsync(cancellationToken), new(request.Key),
			new(request.PushOperationId, request.BaseBranch, request.Title, request.Description), cancellationToken));
		return result.Result is null ? Accepted($"/api/source-control/operations/{result.Value}", result.Value) : result;
	}

	[HttpGet("operations/{id:guid}/pull-request-receipt")]
	public Task<ActionResult<PullRequestReceipt>> PullRequestReceipt(Guid id, CancellationToken cancellationToken) => RunAsync(async () =>
		await store.GetPullRequestReceiptAsync(Context(), id, await RolesAsync(cancellationToken), cancellationToken)
		?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound));

	[HttpGet("operations/{id:guid}/pull-request-status")]
	public Task<ActionResult<PullRequestReceipt>> PullRequestStatus(Guid id, CancellationToken cancellationToken) =>
		RunAsync(async () => await pullRequests.ReadAsync(Context(), id, cancellationToken));

	[HttpGet("operations/{id:guid}")]
	public Task<ActionResult<SourceControlOperation>> Operation(Guid id, CancellationToken cancellationToken) => RunAsync(async () =>
		await store.GetOperationAsync(Context(), id, await RolesAsync(cancellationToken), cancellationToken)
		?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound));

	[HttpGet("operations/{id:guid}/commit-receipt")]
	public Task<ActionResult<CommitReceipt>> Receipt(Guid id, CancellationToken cancellationToken) => RunAsync(async () =>
		await store.GetCommitReceiptAsync(Context(), id, await RolesAsync(cancellationToken), cancellationToken)
		?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound));

	private SourceControlContext Context()
	{
		var subjects = User.FindAll("sub").ToArray();
		var tenants = User.FindAll("tenant_id").ToArray();
		if (User.Identity?.IsAuthenticated != true || subjects.Length != 1 || tenants.Length != 1)
			throw new SourceControlSecurityException(SourceControlErrorCode.Unauthenticated);
		return new(tenants[0].Value, subjects[0].Value);
	}
	private Task<IReadOnlyCollection<string>> RolesAsync(CancellationToken cancellationToken) => actors.ResolveAsync(Context(), cancellationToken);
	private async Task<RepositoryBinding> BindingAsync(Guid id, CancellationToken cancellationToken) =>
		(await store.FindAsync(Context().TenantId, id, cancellationToken))?.Binding
		?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);

	private async Task<ActionResult<T>> RunAsync<T>(Func<Task<T>> execute)
	{
		try { return await execute(); }
		catch (SourceControlSecurityException exception)
		{
			var status = exception.Code switch
			{
				SourceControlErrorCode.Unauthenticated => 401, SourceControlErrorCode.Forbidden => 403, SourceControlErrorCode.NotFound => 404,
				SourceControlErrorCode.RevisionConflict or SourceControlErrorCode.IdempotencyConflict or SourceControlErrorCode.ResultUnknown => 409,
				SourceControlErrorCode.PayloadTooLarge => 413, SourceControlErrorCode.QuotaExceeded => 429,
				SourceControlErrorCode.InvalidInput or SourceControlErrorCode.ContentUnsafe => 400, _ => 503
			};
			return StatusCode(status, new ProblemDetails { Status = status, Title = exception.Code.ToString() });
		}
		catch (ArgumentException) { return BadRequest(new ProblemDetails { Status = 400, Title = "InvalidInput" }); }
		catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested) { return StatusCode(504, new ProblemDetails { Status = 504, Title = "TimedOut" }); }
	}

	public sealed record BindingRequest(string Remote, string? CredentialReference, string DefaultBranch, string ReleaseBranch, string[] ModelRoots);
	public sealed record GrantsRequest(long Revision, RepositoryGrant[] Grants);
	public sealed record SessionRequest(string BaseCommit, Guid Generation);
	public sealed record SnapshotResponse(string Path, Guid Generation, long LocalRevision, byte[] Bytes, string ContentSha256)
	{
		internal ModelSnapshot ToSnapshot() => new(Path, SourceModelKind.Bpmn, Generation, LocalRevision, Bytes);
		internal static SnapshotResponse From(ModelSnapshot snapshot) => new(snapshot.Path, snapshot.DocumentGeneration, snapshot.LocalRevision, snapshot.CopyContent(), snapshot.ContentSha256);
	}
	public sealed record SnapshotsRequest(long Revision, SnapshotResponse[] Snapshots);
	public sealed record DiffBody(string BaseCommit, SnapshotResponse Snapshot);
	public sealed record CommitRequest(string Key, Guid SessionId, long SessionRevision, string BaseCommit, string Message, SnapshotResponse[] Snapshots);
	public sealed record PushRequest(string Key, Guid CommitOperationId, string? ExpectedCommit);
	public sealed record PullRequestRequest(string Key, Guid PushOperationId, string BaseBranch, string Title, string Description);
	public sealed record AdvanceRequest(Guid PushOperationId, long Revision);
}
