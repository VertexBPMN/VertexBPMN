using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using VertexBPMN.SourceControl.Abstractions;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Studio.Components.Modeling;

public partial class SourceControlWorkspacePanel
{
	[Parameter, EditorRequired] public Func<Task<string>> CaptureXml { get; set; } = null!;
	[Parameter] public EventCallback<string> OpenXml { get; set; }
	private readonly CancellationTokenSource _lifetime = new();
	private CancellationTokenSource? _waiting;
	private long _contextVersion;
	private bool _busy;
	private string? _error, _status, _branchCursor, _fileCursor, _historyCursor;
	private StudioGitRepository[] _repositories = [];
	private Guid _repositoryId;
	private string _branch = "", _root = "", _path = "", _message = "";
	private readonly List<string> _branches = [];
	private readonly List<RepositoryFile> _files = [];
	private readonly List<CommitSummary> _history = [];
	private RevisionSelection? _browseRevision;
	private GitCommitId? _baseCommit, _expectedRemote;
	private StudioGitSnapshot? _document;
	private string? _baselineXml, _acceptedXml;
	private Guid? _sessionId, _jobId;
	private long _sessionRevision, _localRevision;
	private StudioGitCommitRequest? _pendingCommit;
	private CommitReceipt? _receipt;
	private Guid? _confirmedPush;
	private StudioGitPullRequestRequest? _pendingPullRequest;
	private PullRequestReceipt? _pullRequestReceipt;
	private PullRequestReceipt? _livePullRequestReceipt;
	private string _prBase = "", _prTitle = "", _prDescription = "";
	private SourceControlOperation? _job;
	private ModelDiff? _diff;
	private string _remote = "", _credential = "", _defaultBranch = "master", _releaseBranch = "release", _modelRoot = "models", _grantsJson = "[]";
	private StudioGitRepository? SelectedRepository => _repositories.SingleOrDefault(x => x.Binding.Id == _repositoryId);
	private bool JobActive => _jobId is not null && (_job is null || _job.State is SourceControlOperationState.Queued or SourceControlOperationState.Running or SourceControlOperationState.Reconciling);

	protected override void OnInitialized() => TenantContext.Changed += TenantChanged;
	private void TenantChanged()
	{
		_contextVersion++;
		_waiting?.Cancel();
		_repositories = [];
		_repositoryId = Guid.Empty;
		DetachDocument();
		_status = "Tenant changed. Editor content is unchanged; the previous Git context is detached.";
		_ = InvokeAsync(StateHasChanged);
	}
	private void DetachDocument()
	{
		_branches.Clear(); _files.Clear(); _history.Clear();
		_browseRevision = null; _document = null; _baseCommit = null; _receipt = null; _diff = null;
		_sessionId = null; _sessionRevision = 0; _localRevision = 0; _jobId = null; _job = null;
		_pendingCommit = null; _baselineXml = null; _acceptedXml = null; _expectedRemote = null; _pushKey = null;
		_confirmedPush = null; _pendingPullRequest = null; _pullRequestReceipt = null;
		_livePullRequestReceipt = null;
		_prBase = ""; _prTitle = ""; _prDescription = "";
		_branchCursor = null; _fileCursor = null; _historyCursor = null; _branch = ""; _root = ""; _path = "";
	}

	private async Task RunAsync(Func<CancellationToken, Task> execute)
	{
		if (_busy) return;
		_busy = true; _error = null;
		var version = _contextVersion;
		using var waiting = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
		_waiting = waiting;
		try { await execute(waiting.Token); }
		catch (OperationCanceledException) { if (version == _contextVersion) _status = "Waiting cancelled. Already accepted jobs may still finish; retry with the same key or refresh status."; }
		catch (HttpRequestException exception) { if (version == _contextVersion) _error = $"Git API request failed ({exception.StatusCode}). Editor content is retained."; }
		catch (Exception) { if (version == _contextVersion) _error = "Git action could not complete. Editor and original source remain available."; }
		finally { _waiting = null; _busy = false; }
	}
	private void CancelWaiting() => _waiting?.Cancel();
	private Task RefreshRepositories() => RunAsync(async token =>
	{
		var availability = await Git.AvailabilityAsync(token);
		token.ThrowIfCancellationRequested();
		if (!availability.Available) { _status = $"Git integration unavailable: {availability.UnavailableReason}. Existing editor functions remain available."; return; }
		var repositories = await Git.RepositoriesAsync(token);
		token.ThrowIfCancellationRequested(); _repositories = repositories;
		_status = "Repository access checked against current Keycloak roles and Vertex grants.";
	});
	private Task SelectRepository(Guid id) => RunAsync(async token =>
	{
		if (_document is not null && !await JS.InvokeAsync<bool>("confirm", token, "Detach the Git context? Editor content remains unchanged. Save/export uncommitted work first.")) return;
		token.ThrowIfCancellationRequested(); DetachDocument(); _repositoryId = id;
		_root = SelectedRepository?.Binding.ModelRoots.FirstOrDefault() ?? "";
		_grantsJson = JsonSerializer.Serialize(SelectedRepository?.Grants ?? []);
		await LoadBranchesAsync(token);
	});
	private Task MoreBranches() => RunAsync(LoadBranchesAsync);
	private async Task LoadBranchesAsync(CancellationToken token)
	{
		var page = await Git.BranchesAsync(_repositoryId, _branchCursor, token);
		token.ThrowIfCancellationRequested(); _branches.AddRange(page.Items); _branchCursor = page.NextCursor;
		if (_branch.Length == 0) _branch = _branches.Contains(SelectedRepository?.Binding.DefaultBranch ?? "") ? SelectedRepository!.Binding.DefaultBranch : _branches.FirstOrDefault() ?? "";
	}
	private Task BrowseFiles() => RunAsync(async token =>
	{
		var revision = await Git.RevisionAsync(_repositoryId, _branch, token);
		token.ThrowIfCancellationRequested(); _browseRevision = revision; _files.Clear(); _fileCursor = null;
		await LoadFilesAsync(token);
	});
	private Task MoreFiles() => RunAsync(LoadFilesAsync);
	private async Task LoadFilesAsync(CancellationToken token)
	{
		var page = await Git.FilesAsync(_repositoryId, _browseRevision!.Commit, _root, _fileCursor, token);
		token.ThrowIfCancellationRequested(); _files.AddRange(page.Items); _fileCursor = page.NextCursor;
	}
	private Task OpenFile() => RunAsync(async token =>
	{
		if (!await JS.InvokeAsync<bool>("confirm", token, "Replace the current editor document? Export/save local changes first. Cancel keeps the editor unchanged.")) return;
		var document = await Git.FileAsync(_repositoryId, _browseRevision!.Commit, _path, Guid.NewGuid(), token);
		token.ThrowIfCancellationRequested();
		using var stream = new MemoryStream(document.Bytes);
		using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
		var xml = await reader.ReadToEndAsync(token);
		token.ThrowIfCancellationRequested(); await OpenXml.InvokeAsync(xml);
		token.ThrowIfCancellationRequested();
		_document = document; _baseCommit = _browseRevision.Commit; _baselineXml = await CaptureXml();
		_confirmedPush = null; _pendingPullRequest = null; _pullRequestReceipt = null;
		_prBase = SelectedRepository!.Binding.DefaultBranch;
		_livePullRequestReceipt = null;
		_sessionId = null; _sessionRevision = 0; _localRevision = 0; _receipt = null; _jobId = null; _job = null; _pendingCommit = null;
		_expectedRemote = null; _diff = null; _history.Clear(); _historyCursor = null;
		_status = "Model opened at a fixed commit. Unchanged commits retain the original bytes; edited XML changes are visible in the diff.";
	});
	private string? _capturedXml;
	private async Task<StudioGitSnapshot> CaptureAsync(CancellationToken token)
	{
		var xml = await CaptureXml(); token.ThrowIfCancellationRequested();
		_capturedXml = xml;
		var bytes = xml == _baselineXml ? _document!.Bytes : Encoding.UTF8.GetBytes(xml);
		return new(_document!.Path, _document.Generation, ++_localRevision, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
	}
	private Task ShowDiff() => RunAsync(async token =>
	{
		var diff = await Git.DiffAsync(_repositoryId, _baseCommit!, await CaptureAsync(token), token);
		token.ThrowIfCancellationRequested(); _diff = diff;
	});
	private Task ShowHistory() => RunAsync(async token => { _history.Clear(); _historyCursor = null; await LoadHistoryAsync(token); });
	private Task MoreHistory() => RunAsync(LoadHistoryAsync);
	private async Task LoadHistoryAsync(CancellationToken token)
	{
		var page = await Git.HistoryAsync(_repositoryId, _baseCommit!, _document!.Path, _historyCursor, token);
		token.ThrowIfCancellationRequested(); _history.AddRange(page.Items); _historyCursor = page.NextCursor;
	}
	private async Task SaveAsync(StudioGitSnapshot snapshot, CancellationToken token)
	{
		if (_sessionId is null)
		{
			var id = await Git.SessionAsync(_repositoryId, _baseCommit!, _document!.Generation, token);
			token.ThrowIfCancellationRequested(); _sessionId = id;
		}
		if (!await Git.SaveAsync(_sessionId.Value, _sessionRevision, snapshot, token)) throw new InvalidOperationException("Snapshot revision conflict.");
		token.ThrowIfCancellationRequested(); _sessionRevision++;
	}
	private Task SaveSnapshot() => RunAsync(async token => { await SaveAsync(await CaptureAsync(token), token); _status = "Draft snapshot saved. No Git commit, push or runtime deployment occurred."; });
	private Task CommitSnapshot() => RunAsync(async token =>
	{
		if (!await JS.InvokeAsync<bool>("confirm", token, "Commit the current editor snapshot to an isolated workbranch? Later edits remain uncommitted.")) return;
		var snapshot = await CaptureAsync(token);
		var acceptedXml = _capturedXml;
		await SaveAsync(snapshot, token);
		token.ThrowIfCancellationRequested(); _acceptedXml = acceptedXml; _pushKey = null;
		_confirmedPush = null; _pendingPullRequest = null; _pullRequestReceipt = null;
		_pendingCommit = new(Guid.NewGuid().ToString("N"), _sessionId!.Value, _sessionRevision, _baseCommit!.Value, _message, [snapshot]);
		_livePullRequestReceipt = null;
		await SubmitPendingCommitAsync(token);
	});
	private Task RetryCommit() => RunAsync(SubmitPendingCommitAsync);
	private async Task SubmitPendingCommitAsync(CancellationToken token)
	{
		var id = await Git.CommitAsync(_repositoryId, _pendingCommit!, token);
		token.ThrowIfCancellationRequested(); _jobId = id; _job = null; _receipt = null;
		_status = "Commit accepted. Refresh its durable status; editor changes remain independent.";
	}
	private string? _pushKey;
	private Task PushCommit() => RunAsync(async token =>
	{
		if (!await JS.InvokeAsync<bool>("confirm", token, "Push only the last confirmed local commit? Current later editor edits are NOT included.")) return;
		_pushKey ??= Guid.NewGuid().ToString("N");
		var id = await Git.PushAsync(_repositoryId, _pushKey, _receipt!.OperationId, _expectedRemote, token);
		token.ThrowIfCancellationRequested(); _jobId = id; _job = null; _status = "Push accepted; refresh durable status. No runtime deployment occurred.";
	});
	private Task RefreshJob() => RunAsync(async token =>
	{
		var job = await Git.OperationAsync(_jobId!.Value, token);
		token.ThrowIfCancellationRequested(); _job = job;
		if (job.Kind == SourceControlOperationKind.PullRequest && job.State == SourceControlOperationState.Succeeded)
		{
			var receipt = await Git.PullRequestReceiptAsync(job.Id, token);
			token.ThrowIfCancellationRequested(); _pullRequestReceipt = receipt; _livePullRequestReceipt = null;
			_status = "Pull request confirmed. Review/merge remain separate; no runtime deployment occurred.";
			return;
		}
		if (job.State == SourceControlOperationState.CommittedLocal)
		{
			var receipt = await Git.ReceiptAsync(job.Id, token);
			token.ThrowIfCancellationRequested(); _receipt = receipt;
			var currentXml = await CaptureXml(); token.ThrowIfCancellationRequested();
			_status = currentXml == _acceptedXml ? "Confirmed snapshot committed locally; not pushed or deployed." : "Confirmed snapshot committed locally. Later editor changes remain UNCOMMITTED.";
		}
		else if (job.State == SourceControlOperationState.Pushed && _receipt is not null)
		{
			if (!await Git.AdvanceAsync(_sessionId!.Value, job.Id, _sessionRevision, token)) throw new InvalidOperationException("Session advance conflict.");
			token.ThrowIfCancellationRequested();
			if (_expectedRemote != _receipt.Commit)
			{
				_sessionRevision++; _expectedRemote = _receipt.Commit; _baseCommit = _receipt.Commit;
				_document = _pendingCommit!.Snapshots[0]; _baselineXml = _acceptedXml;
				_pendingCommit = null; _pushKey = null;
			}
			_status = "Confirmed commit pushed. Later editor edits remain uncommitted; runtime deployment is still separate.";
			_confirmedPush = job.Id;
		}
		else if (job.State is SourceControlOperationState.Conflict or SourceControlOperationState.ResultUnknown or SourceControlOperationState.Failed)
			_status = "Git job did not report success. Keep/export your snapshot; no automatic reset, merge or resend is performed.";
	});
	private Task RefreshPullRequestStatusAsync() => RunAsync(async token =>
	{
		_livePullRequestReceipt = null;
		var receipt = await Git.PullRequestStatusAsync(_pullRequestReceipt!.OperationId, token);
		token.ThrowIfCancellationRequested();
		_livePullRequestReceipt = receipt;
		_status = "GitHub status checked. This is not a review or deployment approval.";
	});
	private Task SubmitPullRequest() => RunAsync(async token =>
	{
		if (_pendingPullRequest is null)
		{
			if (_confirmedPush is null || string.IsNullOrWhiteSpace(_prTitle)) return;
			if (!await JS.InvokeAsync<bool>("confirm", token, "Create a GitHub pull request for the confirmed pushed commit? Later editor edits are NOT included. No merge or deployment will occur.")) return;
			_pendingPullRequest = new(Guid.NewGuid().ToString("N"), _confirmedPush.Value, _prBase, _prTitle, _prDescription);
		}
		var id = await Git.PullRequestAsync(_repositoryId, _pendingPullRequest, token);
		token.ThrowIfCancellationRequested(); _jobId = id; _job = null;
		_status = "Pull request job accepted. Refresh durable status; an unknown result never triggers a blind second create.";
	});
	private Task ExportOriginal() => RunAsync(async token =>
	{
		await JS.InvokeVoidAsync("downloadFile", token, Path.GetFileName(_document!.Path), "data:application/xml;base64," + Convert.ToBase64String(_document.Bytes));
	});
	private Task CreateRepository() => RunAsync(async token =>
	{
		var repository = await Git.CreateAsync(new(_remote, _credential, _defaultBranch, _releaseBranch, [_modelRoot]), token);
		token.ThrowIfCancellationRequested(); _repositories = [.. _repositories, repository];
		_status = "Binding created with Read/Manage only. Explicitly grant Commit/Push before writing.";
	});
	private Task SaveGrants() => RunAsync(async token =>
	{
		var grants = JsonSerializer.Deserialize<RepositoryGrant[]>(_grantsJson) ?? throw new InvalidOperationException("Invalid grants.");
		if (!await JS.InvokeAsync<bool>("confirm", token, "Replace this repository's complete grant list? Changes take effect before subsequent operations.")) return;
		if (!await Git.GrantsAsync(_repositoryId, SelectedRepository!.Revision, grants, token)) throw new InvalidOperationException("Grant revision conflict.");
		var repositories = await Git.RepositoriesAsync(token);
		token.ThrowIfCancellationRequested(); _repositories = repositories; _status = "Repository grants updated.";
	});
	public void Dispose()
	{
		TenantContext.Changed -= TenantChanged;
		_lifetime.Cancel(); _waiting?.Cancel(); _lifetime.Dispose();
	}
}
