namespace VertexBPMN.ServiceDefaults.Security;

/// <summary>
/// Local bridge-network routing only. Keeps the public issuer and browser endpoints intact.
/// Never enables insecure metadata or alters token validation in production.
/// </summary>
public sealed class LocalOidcBackchannelHandler : DelegatingHandler
{
    private readonly Uri _publicAuthority;
    private readonly Uri _internalAuthority;

    public static void Validate(string? backchannelAuthority, string? publicAuthority, bool isOidcTest)
    {
        if (string.IsNullOrWhiteSpace(backchannelAuthority))
		{
			return;
		}

		if (!isOidcTest
            || !Uri.TryCreate(publicAuthority, UriKind.Absolute, out var external)
            || external.Scheme != Uri.UriSchemeHttp || !external.IsLoopback
            || !Uri.TryCreate(backchannelAuthority, UriKind.Absolute, out var internalUri)
            || internalUri.Scheme != Uri.UriSchemeHttp || internalUri.Host != "keycloak" || internalUri.Port != 8080
            || internalUri.AbsolutePath.TrimEnd('/') != external.AbsolutePath.TrimEnd('/')
            || external.AbsolutePath.TrimEnd('/') != "/realms/vertexbpmn"
            || !string.IsNullOrEmpty(internalUri.UserInfo + internalUri.Query + internalUri.Fragment)
            || !string.IsNullOrEmpty(external.UserInfo + external.Query + external.Fragment))
		{
			throw new InvalidOperationException("OIDC BackchannelAuthority is supported only in OidcTest from an HTTP loopback vertexbpmn realm to http://keycloak:8080/realms/vertexbpmn.");
		}
	}

    public LocalOidcBackchannelHandler(string publicAuthority, string internalAuthority, bool isOidcTest, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        Validate(internalAuthority, publicAuthority, isOidcTest);
        _publicAuthority = new Uri(publicAuthority.TrimEnd('/'));
        _internalAuthority = new Uri(internalAuthority.TrimEnd('/'));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (uri is not null && uri.Scheme == _publicAuthority.Scheme
            && uri.Host == _publicAuthority.Host && uri.Port == _publicAuthority.Port
            && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.AbsolutePath == _publicAuthority.AbsolutePath
                || uri.AbsolutePath.StartsWith(_publicAuthority.AbsolutePath + "/", StringComparison.Ordinal)))
        {
            request.RequestUri = new UriBuilder(uri)
            {
                Host = _internalAuthority.Host, Port = _internalAuthority.Port,
                Scheme = _internalAuthority.Scheme
            }.Uri;
        }
        return base.SendAsync(request, cancellationToken);
    }
}
