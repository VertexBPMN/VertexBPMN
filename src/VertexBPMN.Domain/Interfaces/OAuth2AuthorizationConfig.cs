namespace VertexBPMN.Domain.Interfaces;

[method: System.Text.Json.Serialization.JsonConstructor]
[method: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054",
    Justification = "JSON wire contract uses strings; the constructor accepting all three endpoints as Uri provides the typed alternative. Preserve the serialized contract without seven combinatorial overloads.")]
public sealed record OAuth2AuthorizationConfig(
	string AuthorizationUrl,
	string TokenUrl,
	string ClientId,
	string RedirectUri,
	string Scopes)
{
	public OAuth2AuthorizationConfig(Uri authorizationUrl, Uri tokenUrl, string clientId, Uri redirectUri, string scopes)
		: this(authorizationUrl?.OriginalString ?? throw new ArgumentNullException(nameof(authorizationUrl)),
			tokenUrl?.OriginalString ?? throw new ArgumentNullException(nameof(tokenUrl)), clientId,
			redirectUri?.OriginalString ?? throw new ArgumentNullException(nameof(redirectUri)), scopes)
	{ }
}
