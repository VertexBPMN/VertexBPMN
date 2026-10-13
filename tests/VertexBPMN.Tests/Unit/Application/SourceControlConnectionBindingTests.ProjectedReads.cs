using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Theory]
	[InlineData((int)GitReadQueryKind.File)]
	[InlineData((int)GitReadQueryKind.ListFiles)]
	[InlineData((int)GitReadQueryKind.History)]
	[InlineData((int)GitReadQueryKind.Branches)]
	[InlineData((int)GitReadQueryKind.ResolveBranch)]
	public async Task Accepted_projection_restores_its_exact_request_after_restart(int kindValue)
	{
		var kind = (GitReadQueryKind)kindValue;
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var query = QueryFor(kind);
		var revision = query.IsRemoteQuery ? null : ReadRevision;
		var id = await fixture.Store.EnqueueProfiledReadAsync(Context, await ReadTargetAsync(fixture), Admin, new("projected-read"),
			JsonSerializer.SerializeToUtf8Bytes(new AcceptedGitRevisionRead(revision) { Query = query }), registry, Token);
		await using var restarted = fixture.NewContext();
		var store = fixture.NewStore(restarted);
		Assert.Equal(1, await store.TryClaimHttpsRevisionReadAsync(Context, id, "projection", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, registry, Token));
		var restored = await store.ReadClaimedHttpsRevisionReadAsync(Context, id, "projection", 1, Admin, registry, Token);
		Assert.Equal(revision, restored.Revision);
		Assert.Equal(query, restored.Query);
	}

	[Theory]
	[InlineData("traversal")]
	[InlineData("root")]
	[InlineData("generation")]
	[InlineData("moving-revision")]
	[InlineData("branch-injection")]
	[InlineData("unknown")]
	public async Task Malformed_projected_read_cannot_be_claimed(string invalid)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var query = invalid switch
		{
			"traversal" => QueryFor(GitReadQueryKind.File) with { Path = "models/../private.bpmn" },
			"root" => QueryFor(GitReadQueryKind.ListFiles) with { Path = "other" },
			"generation" => QueryFor(GitReadQueryKind.File) with { Generation = Guid.Empty },
			"moving-revision" => QueryFor(GitReadQueryKind.File),
			"branch-injection" => QueryFor(GitReadQueryKind.ResolveBranch) with { Path = "--upload-pack=evil" },
			_ => new((GitReadQueryKind)999)
		};
		var revision = query.IsRemoteQuery || invalid == "moving-revision" ? null : ReadRevision;
		var id = await fixture.Store.EnqueueProfiledReadAsync(Context, await ReadTargetAsync(fixture), Admin, new("invalid-projection"),
			JsonSerializer.SerializeToUtf8Bytes(new AcceptedGitRevisionRead(revision) { Query = query }), registry, Token);
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id,
			"blocked", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).Fence);
	}

	[Fact]
	public async Task New_binding_can_target_an_own_server_without_a_legacy_github_binding()
	{
		await using var fixture = await Fixture.OpenAsync();
		var remote = new Uri("https://git.company.example:8443/team/models.git");
		var profile = fixture.Profile with { Id = Guid.NewGuid(), GitEndpoint = new(remote, "company-network") };
		await fixture.Store.CreateConnectionProfileAsync(Context, Admin, profile, Token);
		var binding = fixture.Binding with { Id = Guid.NewGuid(), Remote = remote, CredentialReference = null };
		var created = await fixture.Store.CreateProfiledBindingAsync(Context, Admin, binding, profile.Id, 1, Token);
		Assert.Equal(remote, created.Binding.Remote);
		Assert.Equal(profile.Id, created.Binding.ConnectionProfileId);
		Assert.Equal(RepositoryPermission.Read | RepositoryPermission.Manage, Assert.Single(created.Grants).Permissions);
		await RejectAsync(SourceControlErrorCode.Forbidden, () => fixture.Store.CreateProfiledBindingAsync(Context, [],
			binding with { Id = Guid.NewGuid() }, profile.Id, 1, Token));
		await RejectAsync(SourceControlErrorCode.RevisionConflict, () => fixture.Store.CreateProfiledBindingAsync(Context, Admin,
			binding with { Id = Guid.NewGuid() }, profile.Id, 2, Token));
	}

	private static AcceptedGitReadQuery QueryFor(GitReadQueryKind kind) => kind switch
	{
		GitReadQueryKind.File => new(kind) { Path = "models/example.bpmn", Generation = Guid.NewGuid() },
		GitReadQueryKind.ListFiles => new(kind) { Path = "models", PageSize = 10 },
		GitReadQueryKind.History => new(kind) { Path = "models/example.bpmn", PageSize = 10 },
		GitReadQueryKind.ResolveBranch => new(kind) { Path = "main" },
		_ => new(kind) { PageSize = 10 }
	};
}
