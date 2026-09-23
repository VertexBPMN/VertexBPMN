using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// Abstraktion für den persistierten Zustand einer OIDC-Sitzung (P4.5).
/// Lokal wird die In-Process-Implementierung verwendet; in Azure kommt der
/// gemeinsame, verschlüsselte PostgreSQL-Store zum Einsatz. Der Store hält für
/// jede Sitzung den verschlüsselten Token-Satz inklusive Hauptprüfsumme (sub)
/// und einer monoton steigenden Revision (für verteiltes Lease/Fencing).
/// </summary>
public interface ISharedOidcSessionStore
{
    /// <summary>Holt den gespeicherten Token-Satz einer Sitzung; null, wenn unbekannt/abgelaufen.</summary>
    Task<StoredOidcSession?> TryGetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt eine Sitzung an oder aktualisiert sie. <paramref name="expectedRevision"/> erlaubt
    /// optimistische Nebenläufigkeit: wird eine fremde neuere Revision erwartet, schlägt der Schreib-
    /// versuch mit <see cref="OidcSessionRevisionConflictException"/> fehl (Fencing).
    /// </summary>
    Task PutAsync(
        string sessionId,
        ClaimsPrincipal principal,
        string accessToken,
        string refreshToken,
        string? idToken,
        DateTimeOffset expiresAt,
        long expectedRevision,
        CancellationToken cancellationToken = default);

    /// <summary>Entfernt eine Sitzung (Logout/Revocation) über alle Replikas hinweg.</summary>
    Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Entfernt abgelaufene Sitzungen (TTL-Reinigung).</summary>
    Task<int> RemoveExpiredAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}

/// <summary>Gespeicherter Sitzungsinhalt mit Versionsprüfung.</summary>
public sealed record StoredOidcSession(
    string SessionId,
    ClaimsPrincipal Principal,
    string AccessToken,
    string RefreshToken,
    string? IdToken,
    DateTimeOffset ExpiresAt,
    long Revision);
