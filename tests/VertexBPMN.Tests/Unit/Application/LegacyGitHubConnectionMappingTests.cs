using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class LegacyGitHubConnectionMappingTests
{
	[Theory]
	[InlineData("https://github.com/owner/models.git")]
	[InlineData("https://github.com/owner/models")]
	[InlineData("https://github.com/owner/models.git/")]
	public void Existing_binding_maps_to_stable_explicit_github_profile_without_altering_legacy_json(string remote)
	{
		var binding = Binding(remote);
		var before = JsonSerializer.Serialize(binding);
		var mapped = Map(binding);
		Assert.Equal(binding.Id, mapped.Id);
		Assert.Equal(binding.TenantId, mapped.TenantId);
		Assert.Equal(1, mapped.Revision);
		Assert.Equal(GitAuthenticationKind.GitHubApp, mapped.Authentication);
		Assert.Equal(binding.CredentialReference, mapped.GitCredentialReference);
		Assert.Equal("legacy-public-git", mapped.GitEndpoint.NetworkPolicyReference);
		Assert.NotNull(mapped.Hosting);
		Assert.Equal("github", mapped.Hosting.ProviderId);
		Assert.Equal("owner/models", mapped.Hosting.RepositoryId);
		Assert.Equal("legacy-public-api", mapped.Hosting.ApiEndpoint.NetworkPolicyReference);
		Assert.Equal(binding.CredentialReference, mapped.Hosting.CredentialReference);
		Assert.Equal(mapped, Map(binding));
		Assert.Equal(before, JsonSerializer.Serialize(binding));
	}

	[Theory]
	[InlineData("https://dev.azure.com/owner/models")]
	[InlineData("https://github.enterprise.example/owner/models.git")]
	[InlineData("https://github.com:8443/owner/models.git")]
	[InlineData("http://github.com/owner/models.git")]
	[InlineData("https://user:secret@github.com/owner/models.git")]
	[InlineData("https://github.com/owner/models.git?token=secret")]
	[InlineData("https://github.com/owner/models.git/child")]
	[InlineData("https://github.com//owner/models.git")]
	[InlineData("https://github.com/owner/models.git//")]
	[InlineData("https://github.com/owner")]
	[InlineData("https://github.com/owner/.git")]
	[InlineData("https://github.com/owner/%6dodels.git")]
	[InlineData("https://github.com/owner/models.git/../other")]
	public void Non_github_or_ambiguous_legacy_targets_do_not_receive_an_implicit_provider(string input)
	{
		var error = Assert.Throws<SourceControlSecurityException>(() => Map(Binding(input)));
		Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
		Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("HttpBasic")]
	[InlineData("GitHubApp ")]
	[InlineData("")]
	[InlineData(null)]
	public void Mapping_requires_verified_existing_credential_type(string? credentialType)
	{
		var binding = Binding("https://github.com/owner/models.git");
		var error = Assert.Throws<SourceControlSecurityException>(() => LegacyGitHubConnectionMapping.Create(binding,
			credentialType!, new(binding.Remote, "legacy-public-git"), new(new Uri("https://api.github.com/"), "legacy-public-api")));
		Assert.Equal(SourceControlErrorCode.CredentialUnavailable, error.Code);
	}

	[Fact]
	public void Migration_cannot_invent_a_credential_or_expand_the_exact_git_endpoint()
	{
		var binding = Binding("https://github.com/owner/models.git");
		Assert.Equal(SourceControlErrorCode.InvalidInput,
			Assert.Throws<SourceControlSecurityException>(() => Map(binding with { CredentialReference = null })).Code);
		Assert.Equal(SourceControlErrorCode.InvalidInput,
			Assert.Throws<SourceControlSecurityException>(() => LegacyGitHubConnectionMapping.Create(binding, "GitHubApp",
				new(new Uri("https://github.com/owner"), "legacy-public-git"), new(new Uri("https://api.github.com/"), "legacy-public-api"))).Code);
	}

	[Theory]
	[InlineData("https://api.github.com/repos/owner/models")]
	[InlineData("https://api.github.com:8443/")]
	[InlineData("https://api.other.example/")]
	[InlineData("https://secret@api.github.com/")]
	[InlineData("relative/api")]
	public void Legacy_api_endpoint_cannot_change_provider_or_leak_credentials(string input)
	{
		var binding = Binding("https://github.com/owner/models.git");
		var error = Assert.Throws<SourceControlSecurityException>(() => LegacyGitHubConnectionMapping.Create(binding,
			"GitHubApp", new(binding.Remote, "legacy-public-git"), new(new Uri(input, UriKind.RelativeOrAbsolute), "legacy-public-api")));
		Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
		Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public void Missing_api_uri_and_policy_are_rejected_with_the_safe_error_contract()
	{
		var binding = Binding("https://github.com/owner/models.git");
		foreach (var endpoint in new[] { new RepositoryEndpoint(null!, "legacy-public-api"),
			new RepositoryEndpoint(new Uri("https://api.github.com/"), "") })
		{
			var error = Assert.Throws<SourceControlSecurityException>(() => LegacyGitHubConnectionMapping.Create(binding,
				"GitHubApp", new(binding.Remote, "legacy-public-git"), endpoint));
			Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
		}
	}

	private static RepositoryBinding Binding(string remote) => new(Guid.NewGuid(), "tenant-a", new Uri(remote),
		"existing-github-app", "master", "release", ["models"]);

	private static RepositoryConnectionProfile Map(RepositoryBinding binding) => LegacyGitHubConnectionMapping.Create(binding,
		"GitHubApp", new(binding.Remote, "legacy-public-git"), new(new Uri("https://api.github.com/"), "legacy-public-api"));
}
