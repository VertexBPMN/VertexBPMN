using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class RepositoryHostingTargetTests
{
	private static readonly SourceControlContext Context = new("tenant", "actor");
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), Context.TenantId,
		new Uri("https://github.com/team/repository.git"), null, "main", "release", ["models"]);
	private static readonly RepositoryConnectionProfile Profile = new(Guid.NewGuid(), Context.TenantId, 1,
		GitTransportKind.Https, GitAuthenticationKind.HttpBasic, new(Binding.Remote, "git-network"), "git-credential", SourceControlCapability.Read)
	{
		Hosting = new("github", new(new Uri("https://api.github.com/repos/team/repository"), "api-network"),
			"team/repository", "api-credential", SourceControlCapability.PullRequest)
	};

	[Theory]
	[InlineData("https://github.com/team/repository.git")]
	[InlineData("https://github.com/team/repository")]
	[InlineData("https://github.com/team/%72epository.git")]
	public void Explicit_Git_and_API_repository_are_bound_without_credential_or_network_I_O(string input)
	{
		var binding = Binding with { Remote = new Uri(input) };
		var profile = Profile with { GitEndpoint = Profile.GitEndpoint with { BaseUri = binding.Remote } };
		var target = new RepositoryHostingTargetPolicy().Resolve(Context, binding, profile);
		Assert.Equal(RepositoryHostingTargetPolicy.LegacyGitHub(Binding), target);
		Assert.Equal("team/repository", target.RepositoryId);
		Assert.Equal("https://api.github.com/repos/team/repository", target.ApiRepositoryUri.AbsoluteUri);
	}

	[Theory]
	[InlineData("https://api.github.com/repos/team/other")]
	[InlineData("https://api.github.com/repos/team/repository/issues")]
	[InlineData("https://github.com/repos/team/repository")]
	[InlineData("https://api.github.com.evil.example/repos/team/repository")]
	[InlineData("https://api.github.com:8443/repos/team/repository")]
	public void Different_API_endpoint_is_rejected(string endpoint)
	{
		var hosting = Profile.Hosting!;
		var changed = Profile with { Hosting = hosting with { ApiEndpoint = hosting.ApiEndpoint with { BaseUri = new Uri(endpoint) } } };
		Assert.Throws<SourceControlSecurityException>(() => new RepositoryHostingTargetPolicy().Resolve(Context, Binding, changed));
	}

	[Theory]
	[InlineData("provider")]
	[InlineData("repository")]
	[InlineData("tenant")]
	[InlineData("absent")]
	public void No_implicit_provider_or_repository_fallback(string change)
	{
		var profile = change switch
		{
			"provider" => Profile with { Hosting = Profile.Hosting! with { ProviderId = "unknown" } },
			"repository" => Profile with { Hosting = Profile.Hosting! with { RepositoryId = "team/other" } },
			"tenant" => Profile with { TenantId = "foreign" },
			_ => Profile with { Hosting = null }
		};
		Assert.Throws<SourceControlSecurityException>(() => new RepositoryHostingTargetPolicy().Resolve(Context, Binding, profile));
	}

	[Theory]
	[InlineData("https://github.com/team/repository/nested.git")]
	[InlineData("https://git.example.com/team/repository.git")]
	[InlineData("https://github.com:8443/team/repository.git")]
	public void Legacy_mapping_rejects_ambiguous_or_different_remotes(string remote)
	{
		Assert.Throws<SourceControlSecurityException>(() => RepositoryHostingTargetPolicy.LegacyGitHub(Binding with { Remote = new Uri(remote) }));
	}
}
