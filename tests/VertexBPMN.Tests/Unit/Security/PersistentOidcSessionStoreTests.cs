using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class PersistentOidcSessionStoreTests
{
	private static ClaimsPrincipal Principal(string sessionId) => new(new ClaimsIdentity(new[]
	{
		new Claim("sub", "user-1"),
		new Claim(ClaimTypes.Name, "Yova"),
		new Claim(OidcSessionTokenStore.SessionIdClaim, sessionId)
	}, "pwd"));

	private static PersistentOidcSessionStore NewStore(SqliteConnection connection)
	{
		var options = new DbContextOptionsBuilder<OidcSessionStoreDbContext>()
			.UseSqlite(connection).Options;
		var dbFactory = new ManualOidcDbFactory(options);
		var protector = new EphemeralDataProtectionProvider();
		return new PersistentOidcSessionStore(dbFactory, protector);
	}

	[Fact]
	public async Task SqliteMigration_Applies_AndPresentsTable()
	{
		// Lokaler Studio-Pfad: die Migration wird über den Migrationspfad auf SQLite angewendet.
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		await using var setup = new OidcSessionStoreDbContext(
			new DbContextOptionsBuilder<OidcSessionStoreDbContext>().UseSqlite(connection).Options);
		await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
		Assert.Empty(await setup.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));

		var store = NewStore(connection);
		const string sessionId = "session-local";
		await store.PutAsync(sessionId, Principal(sessionId), "access", "refresh", "id",
			DateTimeOffset.UtcNow.AddMinutes(30), expectedRevision: 0, TestContext.Current.CancellationToken);
		var read = await store.TryGetAsync(sessionId, TestContext.Current.CancellationToken);
		Assert.NotNull(read);
		Assert.Equal("access", read.AccessToken);
	}


	[Fact]
	public async Task RoundTripsEncryptedSession_AndEnforcesFencing()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		await using (var setup = new OidcSessionStoreDbContext(
			new DbContextOptionsBuilder<OidcSessionStoreDbContext>().UseSqlite(connection).Options))
		{
			await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
		}

		var store = NewStore(connection);
		const string sessionId = "session-roundtrip";

		await store.PutAsync(sessionId, Principal(sessionId), "access-1", "refresh-1", "id-1",
			DateTimeOffset.UtcNow.AddMinutes(30), expectedRevision: 0, TestContext.Current.CancellationToken);
		await store.PutAsync(sessionId, Principal(sessionId), "access-2", "refresh-2", "id-2",
			DateTimeOffset.UtcNow.AddMinutes(30), expectedRevision: 1, TestContext.Current.CancellationToken);

		var read = await store.TryGetAsync(sessionId, TestContext.Current.CancellationToken);
		Assert.NotNull(read);
		Assert.Equal("access-2", read.AccessToken);
		Assert.Equal("refresh-2", read.RefreshToken);
		Assert.Equal("id-2", read.IdToken);
		Assert.Equal(2, read.Revision);
		Assert.Equal("user-1", read.Principal.FindFirstValue("sub"));
		Assert.Equal("Yova", read.Principal.FindFirstValue(ClaimTypes.Name));

		// Stale writer with expectedRevision 1 must lose to the current revision 2.
		await Assert.ThrowsAsync<OidcSessionRevisionConflictException>(() =>
			store.PutAsync(sessionId, Principal(sessionId), "stale", "stale", "stale",
				DateTimeOffset.UtcNow.AddMinutes(30), expectedRevision: 1, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ExpiredSession_IsNotReadable_AndCleanupRemovesIt()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		await using (var setup = new OidcSessionStoreDbContext(
			new DbContextOptionsBuilder<OidcSessionStoreDbContext>().UseSqlite(connection).Options))
		{
			await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
		}

		var store = NewStore(connection);
		const string sessionId = "session-expired";
		await store.PutAsync(sessionId, Principal(sessionId), "access", "refresh", null,
			DateTimeOffset.UtcNow.AddMinutes(-1), expectedRevision: 0, TestContext.Current.CancellationToken);

		Assert.Null(await store.TryGetAsync(sessionId, TestContext.Current.CancellationToken));
		Assert.Equal(1, await store.RemoveExpiredAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
	}

	private sealed class ManualOidcDbFactory(DbContextOptions<OidcSessionStoreDbContext> options)
		: IDbContextFactory<OidcSessionStoreDbContext>
	{
		public OidcSessionStoreDbContext CreateDbContext() => new(options);
		public Task<OidcSessionStoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(new OidcSessionStoreDbContext(options));
	}
}
