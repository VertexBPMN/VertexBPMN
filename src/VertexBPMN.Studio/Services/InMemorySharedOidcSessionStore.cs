using System.Collections.Concurrent;
using System.Security.Claims;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// In-Process-Implementierung des geteilten Session-Stores (Standard-Fallback).
/// Verhält sich funktional identisch zum bisherigen Verhalten und ist die lokale
/// Entwicklungs-Standardwahl (kein Azure-Zugang erforderlich). Revision bleibt
/// der Fencing-Zähler; eine erwartete neuere Revision lässt den Schreibversuch
/// fehlschlagen, damit keine veraltete Token-Rotation eine frische überschreibt.
/// </summary>
public sealed class InMemorySharedOidcSessionStore : ISharedOidcSessionStore
{
    private readonly ConcurrentDictionary<string, StoredOidcSession> _sessions = new(StringComparer.Ordinal);

    public Task<StoredOidcSession?> TryGetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stored = _sessions.TryGetValue(sessionId, out var session) ? session : null;
        if (stored is not null && stored.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            stored = null;
        }
        return Task.FromResult(stored);
    }

    public Task PutAsync(
        string sessionId,
        ClaimsPrincipal principal,
        string accessToken,
        string refreshToken,
        string? idToken,
        DateTimeOffset expiresAt,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hasExisting = _sessions.ContainsKey(sessionId);
        var nextRevision = hasExisting ? expectedRevision + 1 : expectedRevision;
        var candidate = new StoredOidcSession(
            sessionId, principal, accessToken, refreshToken, idToken, expiresAt, nextRevision);

        _sessions.AddOrUpdate(
            sessionId,
            candidate,
            (_, existing) =>
            {
                if (expectedRevision < existing.Revision)
                    throw new OidcSessionRevisionConflictException(
                        $"Session {sessionId} was already updated to revision {existing.Revision}; expected {expectedRevision}.");
                return candidate;
            });

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task<int> RemoveExpiredAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removed = 0;
        foreach (var (id, session) in _sessions)
        {
            if (session.ExpiresAt <= cutoffUtc && _sessions.TryRemove(id, out _))
                removed++;
        }
        return Task.FromResult(removed);
    }
}

/// <summary>Signalisiert eine verlorene Fencing-Wette beim verteilten Session-Schreibzugriff.</summary>
public sealed class OidcSessionRevisionConflictException(string message) : InvalidOperationException(message);
