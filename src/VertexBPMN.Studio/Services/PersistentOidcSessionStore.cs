using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// Persistente Implementierung des geteilten Session-Stores (P4.5) auf PostgreSQL/SQLite.
/// Der Token-Satz wird mit DataProtection verschlüsselt an Ruhe gespeichert. Refresh-
/// Aktualisierungen verwenden eine optimistische Revisionsprüfung (Fencing): ein Schreib-
/// versuch mit veralteter erwarteter Revision schlägt fehl, damit keine Replika eine frische
/// Token-Rotation mit veralteten Tokens überschreibt. Abgelaufene Sitzungen gelten als
/// TTL-nicht-gelesen und werden per <see cref="RemoveExpiredAsync"/> gereinigt.
/// </summary>
public sealed class PersistentOidcSessionStore : ISharedOidcSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<OidcSessionStoreDbContext> _dbFactory;
    private readonly IDataProtector _protector;

    public PersistentOidcSessionStore(
        IDbContextFactory<OidcSessionStoreDbContext> dbFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _dbFactory = dbFactory;
        _protector = dataProtectionProvider.CreateProtector("VertexBPMN.OidcSessions.v1");
    }

    public async Task<StoredOidcSession?> TryGetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Sessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (record is null || record.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return null;

        var payload = Unprotect(record.ProtectedPayload);
        return new StoredOidcSession(
            record.SessionId,
            DeserializePrincipal(payload.PrincipalJson),
            payload.AccessToken,
            payload.RefreshToken,
            payload.IdToken,
            record.ExpiresAtUtc,
            record.Revision);
    }

    public async Task PutAsync(
        string sessionId,
        ClaimsPrincipal principal,
        string accessToken,
        string refreshToken,
        string? idToken,
        DateTimeOffset expiresAt,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        var payload = new SessionPayload(
            SerializePrincipal(principal),
            accessToken,
            refreshToken,
            idToken);
        var protectedPayload = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Sessions.SingleOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (existing is not null)
        {
            if (expectedRevision < existing.Revision)
                throw new OidcSessionRevisionConflictException(
                    $"Session {sessionId} was already updated to revision {existing.Revision}; expected {expectedRevision}.");
            existing.Subject = principal.FindFirstValue("sub") ?? string.Empty;
            existing.ProtectedPayload = protectedPayload;
            existing.ExpiresAtUtc = expiresAt;
            existing.Revision = expectedRevision + 1;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            db.Sessions.Add(new OidcSessionRecord
            {
                SessionId = sessionId,
                Subject = principal.FindFirstValue("sub") ?? string.Empty,
                ProtectedPayload = protectedPayload,
                ExpiresAtUtc = expiresAt,
                Revision = expectedRevision,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Sessions.SingleOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (existing is not null)
        {
            db.Sessions.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> RemoveExpiredAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        // Client-side filter: DateTimeOffset comparisons are not translatable on the SQLite
        // provider (Npgsql supports them server-side, SQLite does not).
        var expired = (await db.Sessions.ToListAsync(cancellationToken))
            .Where(s => s.ExpiresAtUtc <= cutoffUtc)
            .ToList();
        if (expired.Count == 0)
            return 0;
        db.Sessions.RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private SessionPayload Unprotect(byte[] protectedPayload) =>
        JsonSerializer.Deserialize<SessionPayload>(_protector.Unprotect(protectedPayload), JsonOptions)
        ?? throw new InvalidOperationException("The protected OIDC session payload is invalid.");

    private static ClaimsPrincipal DeserializePrincipal(string principalJson)
    {
        var identities = JsonSerializer.Deserialize<SerializedIdentity[]>(principalJson, JsonOptions)
            ?? throw new InvalidOperationException("The stored OIDC session principal is invalid.");
        var result = new List<ClaimsIdentity>();
        foreach (var identity in identities)
        {
            var claimsIdentity = string.IsNullOrWhiteSpace(identity.AuthenticationType)
                ? new ClaimsIdentity()
                : new ClaimsIdentity(identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType);
            foreach (var claim in identity.Claims)
            {
                claimsIdentity.AddClaim(new Claim(
                    claim.Type,
                    claim.Value,
                    claim.ValueType ?? ClaimValueTypes.String,
                    claim.Issuer ?? ClaimsIdentity.DefaultIssuer,
                    claim.OriginalIssuer ?? claim.Issuer ?? ClaimsIdentity.DefaultIssuer));
            }
            result.Add(claimsIdentity);
        }
        return new ClaimsPrincipal(result);
    }

    private static string SerializePrincipal(ClaimsPrincipal principal)
    {
        var claims = principal.Identities
            .Select(identity => new
            {
                AuthenticationType = identity.AuthenticationType,
                NameClaimType = identity.NameClaimType,
                RoleClaimType = identity.RoleClaimType,
                Claims = identity.Claims.Select(c => new { c.Type, c.Value, c.ValueType, c.Issuer, c.OriginalIssuer }).ToArray()
            })
            .ToArray();
        return JsonSerializer.Serialize(claims, JsonOptions);
    }

    private sealed record SessionPayload(
        string PrincipalJson,
        string AccessToken,
        string RefreshToken,
        string? IdToken);

    private sealed record SerializedIdentity(
        string? AuthenticationType,
        string? NameClaimType,
        string? RoleClaimType,
        SerializedClaim[] Claims);

    private sealed record SerializedClaim(
        string Type,
        string Value,
        string? ValueType,
        string? Issuer,
        string? OriginalIssuer);
}
