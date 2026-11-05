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
		if (stored is not null && (stored.AccessToken.Length == 0 || stored.ExpiresAt <= DateTimeOffset.UtcNow))
		{
			stored = null;
		}

		return Task.FromResult(stored);
	}

	public Task PutAsync(string sessionId, ClaimsPrincipal principal, string accessToken, string refreshToken, string? idToken, DateTimeOffset expiresAt, long expectedRevision, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var hasExisting = _sessions.ContainsKey(sessionId);
		var nextRevision = hasExisting ? expectedRevision + 1 : expectedRevision;
		var candidate = new StoredOidcSession(sessionId, principal, accessToken, refreshToken, idToken, expiresAt, nextRevision);
		_sessions.AddOrUpdate(sessionId, candidate, (key, existing) =>
		{
			if (existing.AccessToken.Length == 0 || expectedRevision < existing.Revision)
			{
				throw new OidcSessionRevisionConflictException($"Session {key} was already updated to revision {existing.Revision}; expected {expectedRevision}.");
			}

			return candidate;
		});
		return Task.CompletedTask;
	}

	public Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_sessions.AddOrUpdate(sessionId, key => new StoredOidcSession(key, new ClaimsPrincipal(), string.Empty, string.Empty, null, DateTimeOffset.MaxValue, 0), (_, session) => session with { AccessToken = string.Empty, RefreshToken = string.Empty, IdToken = null, Principal = new ClaimsPrincipal(), Revision = session.Revision + 1 });
		return Task.CompletedTask;
	}

	public async Task<int> RemoveExpiredAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var removed = 0;
		foreach (var(id, session)in _sessions)
		{
			if (session.AccessToken.Length > 0 && session.ExpiresAt <= cutoffUtc)
			{
				await RemoveAsync(id, cancellationToken);
				removed++;
			}
		}

		return removed;
	}
}
