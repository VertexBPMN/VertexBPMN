using System.Globalization;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Theory]
	[InlineData(SourceControlOperationKind.Read)]
	[InlineData(SourceControlOperationKind.Commit)]
	public async Task Bearer_jobs_use_the_protected_profile_contract_and_not_legacy_claiming(SourceControlOperationKind kind)
	{
		await using var fixture = await Fixture.OpenAsync();
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		PersistentSourceControlStore store;
		Guid id;
		if (kind == SourceControlOperationKind.Commit)
		{
			(store, id, _) = await AcceptProfiledCommitAsync(fixture, GitAuthenticationKind.HttpBearer);
		}
		else
		{
			await AssignBearerAsync(fixture);
			store = fixture.Store;
			id = await AcceptRevisionReadAsync(fixture, registry);
		}
		var now = DateTimeOffset.UtcNow;
		Assert.Null(await store.TryClaimAsync(Context.TenantId, id, "legacy", now, TimeSpan.FromMinutes(1), Token));
		var fence = kind == SourceControlOperationKind.Read
			? await store.TryClaimHttpsRevisionReadAsync(Context, id, "bearer", now, TimeSpan.FromMinutes(1), Admin, registry, Token)
			: await store.TryClaimProfiledWriteAsync(Context, id, "bearer", now, TimeSpan.FromMinutes(1), Admin, Token);
		Assert.Equal(1, fence);
		var target = kind == SourceControlOperationKind.Read
			? (await store.ReadClaimedHttpsRevisionReadAsync(Context, id, "bearer", 1, Admin, registry, Token)).Execution.Target
			: (await store.ReadClaimedWriteExecutionAsync(Context, id, "bearer", 1, Admin, Token)).Target;
		Assert.Equal(GitAuthenticationKind.HttpBearer, target.Profile.Authentication);
	}

	[Fact]
	public async Task Bearer_profile_resolves_expiry_and_token_under_current_actor_and_profile_checks()
	{
		await using var fixture = await Fixture.OpenAsync();
		await AssignBearerAsync(fixture);
		var target = await fixture.CaptureAsync();
		var credentials = BearerCredentials();
		using var lease = await Broker(fixture, credentials, new()).IssueAsync(Context, target, await ApproveAsync(fixture), Token);
		Assert.NotNull(lease);
		Assert.Equal(["new-reference/expiresAt", "new-reference/token"], credentials.Requests);
		Assert.Equal($":vertex-bearer-v1:{Environment.NewLine}synthetic-bearer-token{Environment.NewLine}", await ReadLeaseAsync(lease, target.Remote));
	}

	[Theory]
	[InlineData("expired")]
	[InlineData("malformed")]
	[InlineData("wrong-type")]
	[InlineData("foreign-tenant")]
	public async Task Invalid_bearer_metadata_or_expiry_never_resolves_token_material(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		await AssignBearerAsync(fixture);
		var expiry = change == "malformed" ? "not-a-date" : DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture);
		var credentials = BearerCredentials(expiry);
		if (change == "wrong-type")
		{
			credentials.Metadata = credentials.Metadata with { Type = "GitHttpBasic" };
		}
		if (change == "foreign-tenant")
		{
			credentials.Metadata = credentials.Metadata with { TenantId = "foreign-tenant" };
		}
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable, () => Broker(fixture, credentials, new())
			.IssueAsync(Context, target, approved, Token));
		Assert.DoesNotContain("new-reference/token", credentials.Requests, StringComparer.Ordinal);
	}

	[Theory]
	[InlineData("rotation", SourceControlErrorCode.CredentialUnavailable)]
	[InlineData("role", SourceControlErrorCode.Forbidden)]
	[InlineData("profile", SourceControlErrorCode.RevisionConflict)]
	public async Task Changes_during_bearer_resolution_cannot_mint_a_lease(string change, SourceControlErrorCode expected)
	{
		await using var fixture = await Fixture.OpenAsync();
		await AssignBearerAsync(fixture);
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		var credentials = BearerCredentials();
		var actors = new CredentialActors();
		credentials.OnSecret = async key =>
		{
			if (key != "token")
			{
				return;
			}
			switch (change)
			{
				case "rotation":
					credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UtcNow };
					break;
				case "role":
					actors.Roles = ["ReadOnly"];
					break;
				default:
					Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 2, target.Profile with { Revision = 3 }, Token));
					break;
			}
		};
		await RejectAsync(expected, () => Broker(fixture, credentials, actors).IssueAsync(Context, target, approved, Token));
	}

	private static async Task AssignBearerAsync(Fixture fixture)
	{
		var profile = fixture.Profile with { Revision = 2, Authentication = GitAuthenticationKind.HttpBearer };
		Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1, profile, Token));
		Assert.True(await fixture.Store.AssignConnectionProfileAsync(Context, Admin, fixture.Binding.Id, 1, profile.Id, 2, Token));
	}

	private static ProfileCredentials BearerCredentials(string? expiry = null) => new()
	{
		Metadata = new("new-reference", Context.TenantId, "test bearer", "GitHttpBearer", null,
			["token", "expiresAt"], DateTime.UnixEpoch, DateTime.UnixEpoch, null),
		Values = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["token"] = "synthetic-bearer-token",
			["expiresAt"] = expiry ?? DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture)
		}
	};
}
