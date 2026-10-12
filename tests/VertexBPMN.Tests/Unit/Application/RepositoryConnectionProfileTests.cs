using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class RepositoryConnectionProfileTests
{
	private static readonly RepositoryConnectionProfile Basic = new(Guid.NewGuid(), "tenant-a", 1,
		GitTransportKind.Https, GitAuthenticationKind.HttpBasic,
		new(new Uri("https://git.example.com:8443/team"), "corporate-git", "corporate-ca"),
		"git-credential", SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push);

	[Fact]
	public void Git_only_profile_roundtrips_without_hosting_or_github_credentials()
	{
		var restored = JsonSerializer.Deserialize<RepositoryConnectionProfile>(JsonSerializer.Serialize(Basic));
		Assert.NotNull(restored);
		RepositoryConnectionProfilePolicy.Validate(restored);
		Assert.Equal(Basic, restored);
		Assert.Null(restored.Hosting);
	}

	[Fact]
	public void Hosting_has_separate_endpoint_identity_and_credential_reference()
	{
		var profile = Basic with
		{
			Hosting = new("azure-devops", new(new Uri("https://dev.azure.com/organization"), "azure-api"),
				"project/repository-id", "hosting-credential", SourceControlCapability.PullRequest)
		};
		RepositoryConnectionProfilePolicy.Validate(profile);
		Assert.NotEqual(profile.GitCredentialReference, profile.Hosting.CredentialReference, StringComparer.Ordinal);
		Assert.Equal(SourceControlCapability.PullRequest, profile.Hosting.Capabilities);
	}

	[Theory]
	[InlineData(GitAuthenticationKind.HttpBasic)]
	[InlineData(GitAuthenticationKind.HttpBearer)]
	[InlineData(GitAuthenticationKind.GitHubApp)]
	public void Https_authentication_contract_requires_a_credential_reference(GitAuthenticationKind authentication)
	{
		RepositoryConnectionProfilePolicy.Validate(Basic with { Authentication = authentication });
		Reject(Basic with { Authentication = authentication, GitCredentialReference = null });
	}

	[Fact]
	public void Anonymous_profile_is_read_only_and_cannot_resolve_a_credential()
	{
		var profile = Basic with
		{
			Authentication = GitAuthenticationKind.Anonymous,
			GitCredentialReference = null,
			GitCapabilities = SourceControlCapability.Read
		};
		RepositoryConnectionProfilePolicy.Validate(profile);
		Reject(profile with { GitCredentialReference = "unexpected-secret-reference" });
		Reject(profile with { GitCapabilities = Basic.GitCapabilities });
	}

	[Fact]
	public void Ssh_contract_requires_key_authentication_and_explicit_hostkey_trust()
	{
		var profile = Basic with
		{
			Transport = GitTransportKind.Ssh,
			Authentication = GitAuthenticationKind.SshKey,
			GitEndpoint = new(new Uri("ssh://git.example.com:2222/team"), "corporate-ssh", "git-hostkeys")
		};
		RepositoryConnectionProfilePolicy.Validate(profile);
		Reject(profile with { Authentication = GitAuthenticationKind.HttpBasic });
		Reject(profile with { GitEndpoint = profile.GitEndpoint with { TrustProfileReference = null } });
		Reject(Basic with { Authentication = GitAuthenticationKind.SshKey });
	}

	[Theory]
	[InlineData("http://git.example.com/team")]
	[InlineData("file:///team")]
	[InlineData("https://user:secret@git.example.com/team")]
	[InlineData("https://git.example.com/team?token=secret")]
	[InlineData("https://git.example.com/team#fragment")]
	[InlineData("https://git.example.com./team")]
	[InlineData("https://localhost/team")]
	[InlineData("https://127.0.0.1/team")]
	[InlineData("https://[::1]/team")]
	[InlineData("https://git.example.com/")]
	[InlineData("https://git.example.com/team/../other")]
	[InlineData("https://git.example.com/team/%2e%2e/other")]
	[InlineData("https://git.example.com/team/%252e%252e/other")]
	[InlineData("https://git.example.com/team%2fother")]
	[InlineData("https://git.example.com/team%5cother")]
	[InlineData("https://git.example.com/team%0asecret")]
	public void Unsafe_endpoint_inputs_are_rejected_without_echoing_url(string input)
	{
		var error = Reject(Basic with { GitEndpoint = Basic.GitEndpoint with { BaseUri = new Uri(input) } });
		Assert.DoesNotContain(input, error.ToString(), StringComparison.Ordinal);
		Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("https://10.20.30.40:8443/team")]
	[InlineData("https://[fd12:3456::1]:8443/team")]
	[InlineData("https://git.internal:8443/team")]
	public void Private_endpoint_contract_needs_a_named_network_policy_not_a_global_switch(string input)
	{
		var profile = Basic with { GitEndpoint = new(new Uri(input), "private-git-policy", "corporate-ca") };
		RepositoryConnectionProfilePolicy.Validate(profile);
		Reject(profile with { GitEndpoint = profile.GitEndpoint with { NetworkPolicyReference = "" } });
		// Validation is lexical only; GS03 must resolve policy and enforce the actual peer.
	}

	[Fact]
	public void Invalid_identity_revision_enum_and_capability_contracts_fail_closed()
	{
		Reject(Basic with { SchemaVersion = 0 });
		Reject(Basic with { SchemaVersion = RepositoryConnectionProfile.CurrentSchemaVersion + 1 });
		Reject(Basic with { Id = Guid.Empty });
		Reject(Basic with { TenantId = "$global" });
		Reject(Basic with { TenantId = " tenant-a" });
		Reject(Basic with { Revision = 0 });
		Reject(Basic with { Transport = (GitTransportKind)100 });
		Reject(Basic with { Authentication = (GitAuthenticationKind)100 });
		Reject(Basic with { GitCapabilities = SourceControlCapability.Push });
		Reject(Basic with { GitCapabilities = Basic.GitCapabilities | SourceControlCapability.PullRequest });
		Reject(Basic with { GitCapabilities = SourceControlCapability.Read | (SourceControlCapability)128 });
		Reject(Basic with { GitCredentialReference = "credential\nsecret" });
	}

	[Fact]
	public void Missing_endpoint_metadata_is_rejected_as_invalid_input()
	{
		Reject(Basic with { GitEndpoint = null! });
		Reject(Basic with { GitEndpoint = Basic.GitEndpoint with { BaseUri = null! } });
		Reject(Basic with { GitEndpoint = Basic.GitEndpoint with { NetworkPolicyReference = null! } });
		Reject(Basic with { GitEndpoint = Basic.GitEndpoint with { TrustProfileReference = "" } });
	}

	[Theory]
	[InlineData("GitHub")]
	[InlineData("none")]
	[InlineData("provider/type")]
	[InlineData("provider\nsecret")]
	[InlineData("")]
	public void Hosting_identity_and_capabilities_are_validated_independently(string providerId)
	{
		var hosting = new RepositoryHostingConnection("github", new(new Uri("https://api.github.com/"), "github-api"),
			"owner/repository", "github-app", SourceControlCapability.PullRequest);
		RepositoryConnectionProfilePolicy.Validate(Basic with { Hosting = hosting });
		Reject(Basic with { Hosting = hosting with { ProviderId = providerId } });
		Reject(Basic with { Hosting = hosting with { RepositoryId = "" } });
		Reject(Basic with { Hosting = hosting with { CredentialReference = "" } });
		Reject(Basic with { Hosting = hosting with { Capabilities = SourceControlCapability.Read } });
	}

	[Theory]
	[InlineData("https://git.example.com:8443/team")]
	[InlineData("https://git.example.com:8443/team/models.git")]
	public void Repository_scope_matches_exact_host_port_and_path_boundary(string remote)
	{
		Assert.Null(Record.Exception(() =>
			RepositoryConnectionProfilePolicy.DemandRepository(new("tenant-a", "actor-a"), Binding(remote), Basic)));
	}

	[Theory]
	[InlineData("https://other.example.com:8443/team/models.git")]
	[InlineData("https://git.example.com/team/models.git")]
	[InlineData("https://git.example.com:8443/teams/models.git")]
	[InlineData("https://git.example.com:8443/Team/models.git")]
	[InlineData("https://git.example.com:8443/team/../other/models.git")]
	[InlineData("https://git.example.com:8443/team%2fother/models.git")]
	public void Repository_cannot_escape_profile_endpoint(string remote)
	{
		var error = Assert.Throws<SourceControlSecurityException>(() =>
			RepositoryConnectionProfilePolicy.DemandRepository(new("tenant-a", "actor-a"), Binding(remote), Basic));
		Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
	}

	[Fact]
	public void Foreign_tenants_cannot_use_a_connection_profile()
	{
		var binding = Binding("https://git.example.com:8443/team/models.git");
		var actorError = Assert.Throws<SourceControlSecurityException>(() =>
			RepositoryConnectionProfilePolicy.DemandRepository(new("tenant-b", "actor-a"), binding, Basic));
		var bindingError = Assert.Throws<SourceControlSecurityException>(() =>
			RepositoryConnectionProfilePolicy.DemandRepository(new("tenant-a", "actor-a"), binding with { TenantId = "tenant-b" }, Basic));
		Assert.Equal(SourceControlErrorCode.NotFound, actorError.Code);
		Assert.Equal(SourceControlErrorCode.NotFound, bindingError.Code);
	}

	[Fact]
	public void Existing_github_binding_json_keeps_its_public_contract()
	{
		const string json = """
			{"Id":"00000000-0000-0000-0000-000000000001","TenantId":"tenant-a",
			 "Remote":"https://github.com/owner/models.git","CredentialReference":"github-app",
			 "DefaultBranch":"master","ReleaseBranch":"release","ModelRoots":["models"]}
			""";
		var binding = JsonSerializer.Deserialize<RepositoryBinding>(json);
		Assert.NotNull(binding);
		Assert.Equal("github-app", binding.CredentialReference);
		Assert.Equal("github.com", binding.Remote.Host);
		Assert.Equal(["models"], binding.ModelRoots);
		// No implicit profile/default provider is attached until the GS02 migration.
	}

	private static RepositoryBinding Binding(string remote) => new(Guid.NewGuid(), "tenant-a",
		new Uri(remote), "git-credential", "master", "release", ["models"]);

	private static SourceControlSecurityException Reject(RepositoryConnectionProfile profile)
	{
		var error = Assert.Throws<SourceControlSecurityException>(() => RepositoryConnectionProfilePolicy.Validate(profile));
		Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
		return error;
	}
}
