using System.Text.Json;
using System.Text.Json.Nodes;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class SourceControlExecutionTargetTests
{
	private static readonly SourceControlContext Actor = new("tenant-a", "actor-a");
	private static readonly RepositoryConnectionProfile Profile = new(Guid.NewGuid(), Actor.TenantId, 3,
		GitTransportKind.Https, GitAuthenticationKind.HttpBasic,
		new(new Uri("https://git.example.com:8443/team"), "private-git", "corporate-ca"), "git-reference",
		SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push)
	{
		Hosting = new("azure-devops", new(new Uri("https://dev.azure.com/organization"), "azure-api"),
			"project/repository-id", "hosting-reference", SourceControlCapability.PullRequest)
	};
	private static readonly RepositoryAccessSnapshot Access = new(new(Guid.NewGuid(), Actor.TenantId,
		new Uri("https://git.example.com:8443/team/models.git"), "old-binding-reference", "master", "release", ["models"]),
		7, [new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Commit | RepositoryPermission.Push)]);

	[Fact]
	public void Accepted_target_roundtrips_and_preserves_exact_git_and_hosting_identity()
	{
		var accepted = Capture();
		var restored = JsonSerializer.Deserialize<SourceControlExecutionTarget>(JsonSerializer.Serialize(accepted));
		Assert.NotNull(restored);
		Assert.Equal(accepted, restored);
		Assert.Equal(Access.Binding.Id, restored.RepositoryId);
		Assert.Equal(Access.Revision, restored.BindingRevision);
		Assert.Equal(Access.Binding.Remote, restored.Remote);
		Assert.Equal(Profile, restored.Profile);
		Assert.Null(Record.Exception(() => SourceControlExecutionTargetPolicy.DemandCurrent(Actor, Access, Profile, restored)));
	}

	[Fact]
	public void Replacing_configuration_cannot_mutate_an_accepted_target()
	{
		var accepted = Capture();
		var changed = Profile with { Revision = 4, GitCredentialReference = "replacement-reference" };
		Assert.Equal(3, accepted.Profile.Revision);
		Assert.Equal("git-reference", accepted.Profile.GitCredentialReference);
		Assert.Equal(SourceControlErrorCode.RevisionConflict, RejectCurrent(Access, changed, accepted));
	}

	[Fact]
	public void Changed_binding_identity_revision_or_remote_cannot_retarget_a_job()
	{
		var accepted = Capture();
		Assert.Equal(SourceControlErrorCode.RevisionConflict, RejectCurrent(Access with { Revision = 8 }, Profile, accepted));
		Assert.Equal(SourceControlErrorCode.RevisionConflict,
			RejectCurrent(Access with { Binding = Access.Binding with { Id = Guid.NewGuid() } }, Profile, accepted));
		Assert.Equal(SourceControlErrorCode.RevisionConflict,
			RejectCurrent(Access with { Binding = Access.Binding with { Remote = new Uri("https://git.example.com:8443/team/other.git") } }, Profile, accepted));
	}

	[Fact]
	public void Reused_profile_revision_cannot_change_credentials_authentication_trust_or_network_policy()
	{
		var accepted = Capture();
		var hosting = Profile.Hosting!;
		var replacements = new[]
		{
			Profile with { Id = Guid.NewGuid() },
			Profile with { Authentication = GitAuthenticationKind.HttpBearer },
			Profile with { GitCredentialReference = "another-reference" },
			Profile with { GitEndpoint = Profile.GitEndpoint with { BaseUri = new Uri("https://git.example.com:8443/team/") } },
			Profile with { GitEndpoint = Profile.GitEndpoint with { NetworkPolicyReference = "different-network" } },
			Profile with { GitEndpoint = Profile.GitEndpoint with { TrustProfileReference = "different-ca" } },
			Profile with { GitCapabilities = Profile.GitCapabilities | SourceControlCapability.DeleteFile },
			Profile with { Hosting = null },
			Profile with { Hosting = hosting with { ProviderId = "gitlab" } },
			Profile with { Hosting = hosting with { RepositoryId = "another-repository-id" } },
			Profile with { Hosting = hosting with { CredentialReference = "another-hosting-reference" } },
			Profile with { Hosting = hosting with { ApiEndpoint = new(new Uri("https://api.other.example/"), "other-api") } }
		};
		foreach (var replacement in replacements)
		{
			Assert.Equal(SourceControlErrorCode.RevisionConflict, RejectCurrent(Access, replacement, accepted));
		}
	}

	[Fact]
	public void Lost_capability_is_not_a_successful_execution_or_a_silent_fallback()
	{
		var accepted = Capture();
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable,
			RejectCurrent(Access, Profile with { GitCapabilities = SourceControlCapability.Read }, accepted));
		var hostingJob = SourceControlExecutionTargetPolicy.Capture(Actor, Access, Profile, SourceControlCapability.PullRequest);
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable,
			RejectCurrent(Access, Profile with { Hosting = null }, hostingJob));
	}

	[Theory]
	[InlineData(SourceControlCapability.None, SourceControlErrorCode.InvalidInput)]
	[InlineData(SourceControlCapability.VerifyRelease, SourceControlErrorCode.ProviderUnavailable)]
	[InlineData((SourceControlCapability)128, SourceControlErrorCode.ProviderUnavailable)]
	public void Acceptance_requires_explicit_available_capabilities(SourceControlCapability required, SourceControlErrorCode expected)
	{
		var error = Assert.Throws<SourceControlSecurityException>(() =>
			SourceControlExecutionTargetPolicy.Capture(Actor, Access, Profile, required));
		Assert.Equal(expected, error.Code);
	}

	[Fact]
	public void Git_only_read_job_does_not_gain_hosting_or_write_capabilities()
	{
		var anonymous = Profile with
		{
			Authentication = GitAuthenticationKind.Anonymous,
			GitCredentialReference = null,
			GitCapabilities = SourceControlCapability.Read,
			Hosting = null
		};
		var accepted = SourceControlExecutionTargetPolicy.Capture(Actor, Access, anonymous, SourceControlCapability.Read);
		Assert.Null(accepted.Profile.Hosting);
		Assert.Null(accepted.Profile.GitCredentialReference);
		Assert.Null(Record.Exception(() => SourceControlExecutionTargetPolicy.DemandCurrent(Actor, Access, anonymous, accepted)));
	}

	[Fact]
	public void Future_missing_or_malformed_snapshot_contracts_are_rejected()
	{
		var accepted = Capture();
		foreach (var malformed in new[]
		{
			accepted with { SchemaVersion = 0 }, accepted with { SchemaVersion = 2 },
			accepted with { RepositoryId = Guid.Empty }, accepted with { BindingRevision = 0 },
			accepted with { Remote = null! }, accepted with { Profile = null! }
		})
		{
			Assert.Equal(SourceControlErrorCode.ContentUnsafe, RejectCurrent(Access, Profile, malformed));
		}
		Assert.Equal(SourceControlErrorCode.InvalidInput,
			RejectCurrent(Access, Profile, accepted with { RequiredCapabilities = SourceControlCapability.None }));
	}

	[Fact]
	public void Persisted_target_and_profile_cannot_infer_a_missing_schema_version()
	{
		var document = JsonNode.Parse(JsonSerializer.Serialize(Capture()))!.AsObject();
		Assert.True(document.Remove("SchemaVersion"));
		Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SourceControlExecutionTarget>(document.ToJsonString()));

		document["SchemaVersion"] = SourceControlExecutionTarget.CurrentSchemaVersion;
		var profile = document["Profile"]!.AsObject();
		Assert.True(profile.Remove("SchemaVersion"));
		Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SourceControlExecutionTarget>(document.ToJsonString()));
	}

	[Fact]
	public void Foreign_tenant_cannot_replay_a_target_even_when_repository_ids_match()
	{
		var accepted = Capture();
		Assert.Equal(SourceControlErrorCode.NotFound,
			RejectCurrent(Access, Profile with { TenantId = "tenant-b" }, accepted));
		Assert.Equal(SourceControlErrorCode.NotFound,
			RejectCurrent(Access with { Binding = Access.Binding with { TenantId = "tenant-b" } }, Profile, accepted));
		Assert.Equal(SourceControlErrorCode.NotFound,
			RejectCurrent(Access, Profile, accepted with { Profile = Profile with { TenantId = "tenant-b" } }));
	}

	[Fact]
	public void Target_capture_does_not_replace_current_actor_grant_checks()
	{
		var noGrants = Access with { Grants = [] };
		Assert.NotNull(SourceControlExecutionTargetPolicy.Capture(Actor, noGrants, Profile, SourceControlCapability.Push));
		var error = Assert.Throws<SourceControlSecurityException>(() => RepositoryAccessPolicy.Demand(Actor,
			noGrants.Binding, ["ProcessManager"], noGrants.Grants, RepositoryPermission.Push));
		Assert.Equal(SourceControlErrorCode.NotFound, error.Code);
	}

	private static SourceControlExecutionTarget Capture() =>
		SourceControlExecutionTargetPolicy.Capture(Actor, Access, Profile, SourceControlCapability.Push);

	private static SourceControlErrorCode RejectCurrent(RepositoryAccessSnapshot access,
		RepositoryConnectionProfile profile, SourceControlExecutionTarget accepted) =>
		Assert.Throws<SourceControlSecurityException>(() =>
			SourceControlExecutionTargetPolicy.DemandCurrent(Actor, access, profile, accepted)).Code;
}
