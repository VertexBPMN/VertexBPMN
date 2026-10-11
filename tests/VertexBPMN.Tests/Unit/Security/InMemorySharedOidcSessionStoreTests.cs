using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class InMemorySharedOidcSessionStoreTests
{
	private static ClaimsPrincipal Principal(string sessionId) => new(new ClaimsIdentity(new[]
	{
		new Claim("sub", "user-1"),
		new Claim(OidcSessionTokenStore.SessionIdClaim, sessionId)
	}, "pwd"));

	[Fact]
	public async Task ConcurrentPut_OlderRevision_IsRejected_WinsTheFresher()
	{
		var store = new InMemorySharedOidcSessionStore();
		const string sessionId = "session-a";

		// R0 baseline, then a fresher writer advances to R1.
		await store.PutAsync(sessionId, Principal(sessionId), "access-0", "refresh-0", null,
			DateTimeOffset.UtcNow.AddMinutes(30), expectedRevision: 0, cancellationToken: TestContext.Current.CancellationToken);
		await store.PutAsync(sessionId, Principal(sessionId), "access-1", "refresh-1", null,
			DateTimeOffset.UtcNow.AddMinutes(30), expectedRevision: 1, cancellationToken: TestContext.Current.CancellationToken);

		// A stale writer still holding R0 must be fenced out.
		await Assert.ThrowsAsync<OidcSessionRevisionConflictException>(async () =>
			await store.PutAsync(sessionId, Principal(sessionId), "stale-access", "stale-refresh", null,
				DateTimeOffset.UtcNow.AddMinutes(30), expectedRevision: 0, cancellationToken: TestContext.Current.CancellationToken));

		var stored = await store.TryGetAsync(sessionId, cancellationToken: TestContext.Current.CancellationToken);
		Assert.NotNull(stored);
		Assert.Equal("access-1", stored.AccessToken);
		Assert.Equal("refresh-1", stored.RefreshToken);
	}

	[Fact]
	public async Task ExpiredSession_IsNotReadable_AndAuditCleanupRemovesIt()
	{
		var store = new InMemorySharedOidcSessionStore();
		const string sessionId = "session-expired";

		await store.PutAsync(sessionId, Principal(sessionId), "access", "refresh", "id",
			DateTimeOffset.UtcNow.AddMinutes(-1), expectedRevision: 0, cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(1, await store.RemoveExpiredAsync(DateTimeOffset.UtcNow, cancellationToken: TestContext.Current.CancellationToken));
		Assert.Null(await store.TryGetAsync(sessionId, cancellationToken: TestContext.Current.CancellationToken));
	}
}
