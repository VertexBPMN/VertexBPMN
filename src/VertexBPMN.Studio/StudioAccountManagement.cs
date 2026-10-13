namespace VertexBPMN.Studio;

internal sealed class StudioAccountManagement
{
	public StudioAccountManagement(string? configuredUrl, string environmentName)
	{
		if (string.IsNullOrWhiteSpace(configuredUrl))
		{
			return;
		}

		if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
			|| string.IsNullOrEmpty(uri.Host)
			|| !string.IsNullOrEmpty(uri.UserInfo)
			|| !(uri.Scheme == Uri.UriSchemeHttps
				|| (environmentName.Equals("OidcTest", StringComparison.OrdinalIgnoreCase)
					&& uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
		{
			throw new InvalidOperationException("StudioAuthentication:AccountManagementUrl must be an absolute HTTPS URL without user information; only OidcTest permits HTTP on loopback.");
		}

		Url = uri.AbsoluteUri;
	}

	public string? Url { get; }
}
