using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Only explicit, implemented repository mappings may associate Git and hosting endpoints.</summary>
public sealed class RepositoryHostingTargetPolicy : IRepositoryHostingTargetResolver
{
	public RepositoryHostingTarget Resolve(SourceControlContext context, RepositoryBinding binding, RepositoryConnectionProfile profile)
	{
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, profile);
		var hosting = profile.Hosting ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		if (hosting.ProviderId == AzureDevOpsHostingTargetPolicy.ProviderId)
		{
			return AzureDevOpsHostingTargetPolicy.Resolve(binding, hosting);
		}
		if (hosting.ProviderId != "github")
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var target = GitHubTarget(binding.Remote);
		if (hosting.RepositoryId != target.RepositoryId
			|| hosting.ApiEndpoint.BaseUri.AbsoluteUri != target.ApiRepositoryUri.AbsoluteUri)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		return target;
	}

	public static RepositoryHostingTarget LegacyGitHub(RepositoryBinding binding)
	{
		ArgumentNullException.ThrowIfNull(binding);
		if (binding.ConnectionProfileId is not null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		return GitHubTarget(binding.Remote);
	}

	private static RepositoryHostingTarget GitHubTarget(Uri remote)
	{
		RepositoryConnectionProfilePolicy.ValidateHttpsRepositoryScope(remote);
		if (remote.IdnHost != "github.com" || !remote.IsDefaultPort)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var repository = remote.AbsolutePath.Trim('/');
		if (repository.EndsWith(".git", StringComparison.Ordinal))
		{
			repository = repository[..^4];
		}
		var segments = repository.Split('/');
		if (segments.Length != 2 || segments.Any(segment => string.IsNullOrEmpty(segment)
			|| segment is "." or ".." || !segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		return new("github", repository, new Uri($"https://api.github.com/repos/{repository}"), new Uri($"https://github.com/{repository}"));
	}
}
