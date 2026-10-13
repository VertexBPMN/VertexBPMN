using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>
/// Explicit upgrade mapping for verified GitHub-App bindings only. This does not
/// persist profiles, resolve credentials or register the administrative network policies.
/// </summary>
public static class LegacyGitHubConnectionMapping
{
	public static RepositoryConnectionProfile Create(RepositoryBinding binding, string credentialType,
		RepositoryEndpoint gitEndpoint, RepositoryEndpoint apiEndpoint)
	{
		ArgumentNullException.ThrowIfNull(binding);
		ArgumentNullException.ThrowIfNull(gitEndpoint);
		ArgumentNullException.ThrowIfNull(apiEndpoint);
		if (credentialType != "GitHubApp")
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}

		var profile = new RepositoryConnectionProfile(binding.Id, binding.TenantId, 1,
			GitTransportKind.Https, GitAuthenticationKind.GitHubApp, gitEndpoint,
			binding.CredentialReference, SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push);
		RepositoryConnectionProfilePolicy.Validate(profile);
		RepositoryConnectionProfilePolicy.DemandRepository(new(binding.TenantId, "legacy-mapping"), binding, profile);
		if (binding.Remote.IdnHost != "github.com" || binding.Remote.Port != 443
			|| gitEndpoint.BaseUri.IdnHost != "github.com" || gitEndpoint.BaseUri.Port != 443
			|| gitEndpoint.BaseUri.AbsoluteUri != binding.Remote.AbsoluteUri
			|| binding.Remote.OriginalString.Contains('%', StringComparison.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		var path = binding.Remote.AbsolutePath;
		var repository = path.Trim('/');
		if (path != "/" + repository && path != "/" + repository + "/")
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		if (repository.EndsWith(".git", StringComparison.Ordinal))
		{
			repository = repository[..^4];
		}

		var segments = repository.Split('/');
		if (segments.Length != 2 || segments.Any(segment => segment.Length == 0 || segment is "." or ".."
			|| segment.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		profile = profile with
		{
			Hosting = new("github", apiEndpoint, repository, binding.CredentialReference!, SourceControlCapability.PullRequest)
		};
		RepositoryConnectionProfilePolicy.Validate(profile);
		if (apiEndpoint.BaseUri.AbsoluteUri != "https://api.github.com/")
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		return profile;
	}
}
