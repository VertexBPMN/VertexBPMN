using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Explicit Azure DevOps Services mapping; Server and legacy visualstudio.com endpoints require separate adapters.</summary>
public static class AzureDevOpsHostingTargetPolicy
{
	public const string ProviderId = "azure-devops-services";
	public const string ApiVersion = "7.1";

	public static RepositoryHostingTarget Resolve(RepositoryBinding binding, RepositoryHostingConnection hosting)
	{
		RepositoryConnectionProfilePolicy.ValidateHttpsRepositoryScope(binding.Remote);
		var remote = binding.Remote;
		var parts = remote.AbsolutePath.Split('/');
		if (remote.IdnHost != "dev.azure.com" || !remote.IsDefaultPort || parts.Length != 5 || parts[3] != "_git"
			|| parts.Where((_, index) => index is 1 or 2 or 4).Any(part => !ValidSegment(part)))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		var identity = hosting.RepositoryId.Split('/');
		if (identity.Length != 3 || identity[0] != parts[1]
			|| !Guid.TryParseExact(identity[1], "D", out var project) || project == Guid.Empty || identity[1] != project.ToString("D")
			|| !Guid.TryParseExact(identity[2], "D", out var repository) || repository == Guid.Empty || identity[2] != repository.ToString("D"))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput); }
		var api = new Uri($"https://dev.azure.com/{parts[1]}/{parts[2]}/_apis/git/repositories/{repository:D}");
		if (hosting.ProviderId != ProviderId || hosting.ApiEndpoint.BaseUri.AbsoluteUri != api.AbsoluteUri)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		return new(ProviderId, hosting.RepositoryId, api, remote) { ApiVersion = AzureDevOpsHostingTargetPolicy.ApiVersion };
	}

	private static bool ValidSegment(string segment)
	{
		var decoded = Uri.UnescapeDataString(segment);
		return decoded.Length is > 0 and <= 120 && decoded is not ("." or "..")
			&& !decoded.Any(character => char.IsControl(character) || character is '/' or '\\' or '%' or '?' or '#' or ':' or '"' or '\'')
			&& segment == Uri.EscapeDataString(decoded);
	}
}
