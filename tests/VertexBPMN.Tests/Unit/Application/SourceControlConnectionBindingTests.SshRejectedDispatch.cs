using Microsoft.EntityFrameworkCore;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Production_job_host_selects_SSH_and_rejects_tampered_work_before_workspace_or_network_use()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		await using var harness = await SshReadRunnerHarness.OpenAsync(fixture);
		var registry = SshExecutionRegistry(() => SshConfigurationOptions(fixture));
		var id = await fixture.Store.EnqueueSshReadAsync(Context, await ReadTargetAsync(fixture), Admin, new("ssh-host"),
			new(null) { Query = new(GitReadQueryKind.Branches) { PageSize = 10 } }, registry, Token);
		await fixture.Db.SourceControlOperations.Where(row => row.Id == id)
			.ExecuteUpdateAsync(update => update.SetProperty(row => row.ProtectedExecutionTarget, "invalid-ciphertext"), Token);
		using var host = harness.CreateJobHost();
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
		deadline.CancelAfter(TimeSpan.FromSeconds(10));
		try
		{
			await host.StartAsync(deadline.Token);
			while ((await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, deadline.Token)).State
				== (int)SourceControlOperationState.Queued)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token);
			}
		}
		finally
		{
			await host.StopAsync(Token);
		}
		var actual = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token);
		Assert.Equal((int)SourceControlOperationState.Failed, actual.State);
		Assert.Equal((int)SourceControlErrorCode.ContentUnsafe, actual.ErrorCode);
		Assert.Equal(0, actual.Fence);
		Assert.False(Directory.Exists(harness.Root));
	}

	[Theory]
	[InlineData("unstarted")]
	[InlineData("claimed")]
	[InlineData("transient")]
	public async Task Ssh_dispatch_rejection_finishes_only_the_unchanged_unstarted_job(string scenario)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		Assert.True(await fixture.AssignAsync());
		var registry = SshExecutionRegistry(() => SshConfigurationOptions(fixture));
		var id = await fixture.Store.EnqueueSshReadAsync(Context, await ReadTargetAsync(fixture), Admin, new("ssh-rejection"), new(ReadRevision), registry, Token);
		var observed = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token);
		if (scenario == "claimed")
		{
			Assert.Equal(1, await fixture.Store.TryClaimSshReadAsync(Context, id, "other-worker", DateTimeOffset.UtcNow,
				TimeSpan.FromMinutes(1), Admin, registry, Token));
		}
		var reason = scenario == "transient" ? SourceControlErrorCode.CredentialUnavailable : SourceControlErrorCode.Forbidden;
		await fixture.Store.RejectUnstartedProfiledDispatchAsync(observed, reason, Token);
		var actual = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token);
		Assert.Equal(scenario switch
		{
			"unstarted" => SourceControlOperationState.Failed,
			"claimed" => SourceControlOperationState.Running,
			_ => SourceControlOperationState.Queued
		}, (SourceControlOperationState)actual.State);
		Assert.Equal(scenario == "unstarted" ? (int?)reason : null, actual.ErrorCode);
		Assert.Equal(observed.ProtectedRequest, actual.ProtectedRequest);
		Assert.Equal(observed.ProtectedExecutionTarget, actual.ProtectedExecutionTarget);
	}
}
