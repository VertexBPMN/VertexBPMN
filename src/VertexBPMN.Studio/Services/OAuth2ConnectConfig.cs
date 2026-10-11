namespace VertexBPMN.Studio.Services;

internal sealed record OAuth2ConnectConfig(
    string AuthorizationUrl,
    string TokenUrl,
    string ClientId,
    string RedirectUri,
    string Scopes);
