using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed partial class GitHttpsTransportTests
{
	[Theory]
	[InlineData("sha1")]
	[InlineData("sha256")]
	public async Task Resolved_remote_branch_pins_model_bytes_across_head_changes(string objectFormat)
	{
		await using var fixture = await HttpsFixture.CreateAsync(objectFormat: objectFormat);
		using var lease = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(3));
		var token = TestContext.Current.CancellationToken;
		var context = new SourceControlContext("test", "actor");
		var binding = new RepositoryBinding(Guid.NewGuid(), context.TenantId, fixture.Remote, null, "master", "release", ["models"]);
		var selected = await fixture.Runner.ResolveRemoteBranchLocalAcceptanceAsync(fixture.Workspace, context, binding,
			"master", lease, fixture.CaFile, token);
		Assert.Equal(binding.Id, selected.RepositoryId);
		Assert.Equal("master", selected.Branch);
		Assert.Equal(await fixture.RemoteHeadAsync("master"), selected.Commit);
		var changed = await fixture.AddRemoteModelRevisionAsync("changed-after-selection");
		Assert.NotEqual(selected.Commit, changed);
		await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease, fixture.CaFile, token, selected.Commit);
		var snapshot = await fixture.Runner.ReadModelAsync(fixture.Workspace, binding,
			new(selected.Commit, "models/example.bpmn"), Guid.NewGuid(), token);
		Assert.Equal(fixture.ModelBytes, snapshot.CopyContent());
		var refreshed = await fixture.Runner.ResolveRemoteBranchLocalAcceptanceAsync(fixture.Workspace, context, binding,
			"master", lease, fixture.CaFile, token);
		Assert.Equal(changed, refreshed.Commit);
		Assert.Equal(0, fixture.PushRequests);
		Assert.True(fixture.AuthenticatedRequests > 0);

		var absent = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.ResolveRemoteBranchLocalAcceptanceAsync(
			fixture.Workspace, context, binding, "missing", lease, fixture.CaFile, token));
		Assert.Equal(SourceControlErrorCode.NotFound, absent.Code);
		var requests = fixture.AuthenticatedRequests;
		var foreign = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.ResolveRemoteBranchLocalAcceptanceAsync(
			fixture.Workspace, new("foreign", "actor"), binding, "master", lease, fixture.CaFile, token));
		Assert.Equal(SourceControlErrorCode.NotFound, foreign.Code);
		foreach (var branch in new[] { "--upload-pack=bad", "HEAD", "master*", "master\nother" })
		{
			var invalid = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.ResolveRemoteBranchLocalAcceptanceAsync(
				fixture.Workspace, context, binding, branch, lease, fixture.CaFile, token));
			Assert.Equal(SourceControlErrorCode.InvalidInput, invalid.Code);
		}
		Assert.Equal(requests, fixture.AuthenticatedRequests);
		var blocked = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.ResolveRemoteBranchAsync(
			fixture.Workspace, context, binding, "master", lease, token));
		Assert.Equal(SourceControlErrorCode.InvalidInput, blocked.Code);
		Assert.Equal(requests, fixture.AuthenticatedRequests);
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Runner.ResolveRemoteBranchLocalAcceptanceAsync(
			fixture.Workspace, context, binding, "master", lease, fixture.CaFile, cancelled.Token));
		Assert.Equal(requests, fixture.AuthenticatedRequests);
	}
}
