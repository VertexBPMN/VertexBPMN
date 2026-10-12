using Microsoft.EntityFrameworkCore;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Theory]
	[InlineData("unstarted")]
	[InlineData("claimed")]
	[InlineData("transient")]
	public async Task Dispatch_rejection_finishes_only_the_unchanged_unstarted_job(string scenario)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptRevisionReadAsync(fixture, registry);
		var observed = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token);
		if (scenario == "claimed")
		{
			Assert.Equal(1, await fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id, "other-worker", DateTimeOffset.UtcNow,
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
