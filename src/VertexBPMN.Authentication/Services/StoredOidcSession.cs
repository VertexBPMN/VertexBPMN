using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace VertexBPMN.Studio.Services;

/// <summary>Gespeicherter Sitzungsinhalt mit Versionsprüfung.</summary>
public sealed record StoredOidcSession(
    string SessionId,
    ClaimsPrincipal Principal,
    string AccessToken,
    string RefreshToken,
    string? IdToken,
    DateTimeOffset ExpiresAt,
    long Revision);
