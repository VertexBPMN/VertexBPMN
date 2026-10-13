using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Public_read_facade_accepts_SSH_and_returns_only_its_confirmed_typed_result()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var configuration = SshConfigurationOptions(fixture);
		var monitor = new Mock<IOptionsMonitor<SourceControlOptions>>();
		monitor.SetupGet(value => value.CurrentValue).Returns(() => configuration);
		var id = await fixture.Store.EnqueueProfiledReadAsync(Context, fixture.Binding.Id, Admin, new("ssh-api"),
			new(GitReadQueryKind.Branches, null, null, 10, null, Guid.Empty), monitor.Object, Token);
		Assert.Equal(4, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).ExecutionContractVersion);
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => fixture.Store.GetProfiledReadResultAsync(Context, id, Admin, monitor.Object, Token));
		var registry = SshExecutionRegistry(() => configuration);
		Assert.Equal(1, await fixture.Store.TryClaimSshReadAsync(Context, id, "facade", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.True(await fixture.Store.FinishSshReadAsync(Context, id, "facade", 1, Admin, registry,
			new SourceControlPage<string>(["main", "release"], null), Token));
		await using var restarted = fixture.NewContext();
		var store = fixture.NewStore(restarted);
		var result = await store.GetProfiledReadResultAsync(Context, id, Admin, monitor.Object, Token);
		Assert.Equal("Branches", result.Kind);
		Assert.Equal(id, result.OperationId);
		Assert.Equal(["main", "release"], result.Value.GetProperty("Items").EnumerateArray().Select(item => item.GetString()).ToArray());
		await RejectAsync(SourceControlErrorCode.NotFound,
			() => store.GetProfiledReadResultAsync(new(Context.TenantId, "foreign"), id, Admin, monitor.Object, Token));
		configuration = new()
		{
			Enabled = true,
			SshNetworkPolicies = configuration.SshNetworkPolicies,
			SshTrustProfiles = [configuration.SshTrustProfiles[0] with { Revision = 2 }]
		};
		await RejectAsync(SourceControlErrorCode.RevisionConflict, () => store.GetProfiledReadResultAsync(Context, id, Admin, monitor.Object, Token));
	}
}
