using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>
/// Validates connection contracts without I/O. This does not resolve network/trust
/// references, approve DNS addresses, release secrets or establish repository grants.
/// </summary>
public static class RepositoryConnectionProfilePolicy
{
	private const SourceControlCapability GitCapabilities = SourceControlCapability.Read
		| SourceControlCapability.Commit | SourceControlCapability.Push
		| SourceControlCapability.DeleteFile | SourceControlCapability.RenameFile;
	private const SourceControlCapability HostingCapabilities = SourceControlCapability.PullRequest
		| SourceControlCapability.VerifyRelease;

	public static void Validate(RepositoryConnectionProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);
		if (profile.SchemaVersion != RepositoryConnectionProfile.CurrentSchemaVersion
			|| profile.Id == Guid.Empty || !ValidReference(profile.TenantId, 64)
			|| profile.TenantId == "$global" || profile.Revision <= 0
			|| !Enum.IsDefined(profile.Transport) || !Enum.IsDefined(profile.Authentication)
			|| (profile.GitCapabilities & ~GitCapabilities) != SourceControlCapability.None
			|| (profile.GitCapabilities & SourceControlCapability.Read) == SourceControlCapability.None)
		{
			Reject();
		}

		var scheme = profile.Transport == GitTransportKind.Https ? Uri.UriSchemeHttps : "ssh";
		ValidateEndpoint(profile.GitEndpoint, scheme);
		if (profile.GitEndpoint.BaseUri.AbsolutePath is "" or "/")
		{
			Reject();
		}

		if (profile.Transport == GitTransportKind.Ssh)
		{
			if (profile.Authentication != GitAuthenticationKind.SshKey
				|| !ValidReference(profile.GitEndpoint.TrustProfileReference))
			{
				Reject();
			}
		}
		else if (profile.Authentication == GitAuthenticationKind.SshKey)
		{
			Reject();
		}

		if (profile.Authentication == GitAuthenticationKind.Anonymous)
		{
			if (profile.GitCredentialReference is not null || profile.GitCapabilities != SourceControlCapability.Read)
			{
				Reject();
			}
		}
		else if (!ValidReference(profile.GitCredentialReference))
		{
			Reject();
		}

		if (profile.Hosting is { } hosting)
		{
			ValidateEndpoint(hosting.ApiEndpoint, Uri.UriSchemeHttps);
			if (!ValidProviderId(hosting.ProviderId) || !ValidReference(hosting.RepositoryId)
				|| !ValidReference(hosting.CredentialReference)
				|| hosting.Capabilities == SourceControlCapability.None
				|| (hosting.Capabilities & ~HostingCapabilities) != SourceControlCapability.None)
			{
				Reject();
			}
		}
	}

	/// <summary>Validate an administrative HTTPS repository scope without resolving its host.</summary>
	public static void ValidateHttpsRepositoryScope(Uri scope)
	{
		ValidateUri(scope, Uri.UriSchemeHttps);
		if (scope.AbsolutePath is "" or "/")
		{
			Reject();
		}
	}

	/// <summary>Endpoint scoping is additional to, never a replacement for, repository ACL checks.</summary>
	public static void DemandRepository(SourceControlContext context, RepositoryBinding binding,
		RepositoryConnectionProfile profile)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(binding);
		Validate(profile);
		if (context.TenantId != profile.TenantId || binding.TenantId != profile.TenantId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}

		var endpoint = profile.GitEndpoint.BaseUri;
		ValidateUri(binding.Remote, endpoint.Scheme);
		var root = endpoint.AbsolutePath.TrimEnd('/');
		if (!string.Equals(binding.Remote.IdnHost, endpoint.IdnHost, StringComparison.OrdinalIgnoreCase)
			|| binding.Remote.Port != endpoint.Port
			|| (binding.Remote.AbsolutePath != root
				&& !binding.Remote.AbsolutePath.StartsWith(root + "/", StringComparison.Ordinal)))
		{
			Reject();
		}
	}

	private static void ValidateEndpoint(RepositoryEndpoint endpoint, string scheme)
	{
		if (endpoint is null)
		{
			Reject();
		}

		if (!ValidReference(endpoint.NetworkPolicyReference)
			|| (endpoint.TrustProfileReference is not null && !ValidReference(endpoint.TrustProfileReference)))
		{
			Reject();
		}

		ValidateUri(endpoint.BaseUri, scheme);
	}

	private static void ValidateUri(Uri target, string scheme)
	{
		if (target is null)
		{
			Reject();
		}

		if (!target.IsAbsoluteUri || target.Scheme != scheme
			|| target.Port is < 1 or > 65535 || target.UserInfo.Length != 0
			|| target.Query.Length != 0 || target.Fragment.Length != 0
			|| target.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)
			|| target.IsLoopback || target.Host.EndsWith('.')
			|| target.OriginalString.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\'))
		{
			Reject();
		}

		// Uri normalizes literal dot segments. Inspect the original path as well, before
		// normalization can turn an untrusted traversal into an apparently safe scope.
		var authorityStart = target.OriginalString.IndexOf("://", StringComparison.Ordinal) + 3;
		var pathStart = target.OriginalString.IndexOf('/', authorityStart);
		if (pathStart < 0)
		{
			return;
		}

		var path = target.OriginalString[pathStart..];
		foreach (var segment in path.Split('/'))
		{
			var decoded = Uri.UnescapeDataString(segment);
			if (decoded is "." or ".."
				|| decoded.Any(c => char.IsControl(c) || c is '/' or '\\' or '%' or '?' or '#'))
			{
				Reject();
			}
		}
	}

	private static bool ValidReference(string? value, int maximumLength = 240) =>
		!string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength && value == value.Trim()
		&& !value.Any(char.IsControl);

	private static bool ValidProviderId(string value) => ValidReference(value, 64)
		&& value != "none" && char.IsAsciiLetterLower(value[0])
		&& value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
