namespace VertexBPMN.Domain.Interfaces;

[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record OAuth2AuthorizationStart(
	string RedirectUrl,
	string State)
{
	public OAuth2AuthorizationStart(Uri redirectUrl, string state)
		: this(redirectUrl?.OriginalString ?? throw new ArgumentNullException(nameof(redirectUrl)), state) { }
}
