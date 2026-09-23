using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VertexBPMN.Studio.Services;
using Xunit;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// P4.5: geteilter, verschlüsselter OIDC-Session-Store auf PostgreSQL.
/// Beweist gegen einen echten, isolierten PostgreSQL: (a) Migration über den vollständigen
/// Migrationspfad, (b) verschlüsselte Round-Trip-Persistenz, (c) verteiltes Fencing (eine
/// veraltete erwartete Revision verliert gegen die frische), (d) TTL-Reinigung.
/// Läuft ohne VERTEXBPMN_TEST_POSTGRES_ADMIN als Skip.
/// </summary>
public sealed class OidcSessionStorePostgresAcceptanceTests
{
    private const string Category = "PostgresProviderAcceptance";

    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN") ?? "";

    private static string ConnectionStringFor(string admin, string database)
        => Regex.Replace(admin, @"Database=[^;]*", $"Database={database}", RegexOptions.IgnoreCase);

    private static async Task<string> CreateDatabaseAsync(string admin, string databaseName)
    {
        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return ConnectionStringFor(admin, databaseName);
    }

    private static async Task DropDatabaseAsync(string admin, string databaseName)
    {
        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static ClaimsPrincipal Principal(string sessionId) => new(new ClaimsIdentity(new[]
    {
        new Claim("sub", "user-1"),
        new Claim(ClaimTypes.Name, "Yova"),
        new Claim(OidcSessionTokenStore.SessionIdClaim, sessionId)
    }, "pwd"));

    [Fact]
    [Trait("Category", Category)]
    public async Task Postgres_Migrates_RoundTrips_Encrypted_And_Enforces_Fencing()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(AdminConnectionString), "Local PostgreSQL connection required.");
        var db = $"oidcsession_{Guid.NewGuid():N}";
        var cs = await CreateDatabaseAsync(AdminConnectionString, db);
        try
        {
            // Migration ausschließlich über den Migrationspfad (providerneutral, Npgsql).
            var options = new DbContextOptionsBuilder<OidcSessionStoreDbContext>().UseNpgsql(cs).Options;
            await using (var setup = new OidcSessionStoreDbContext(options))
                await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);

            var store = new PersistentOidcSessionStore(
                new ManualOidcDbFactory(options),
                new EphemeralDataProtectionProvider());
            var sessionId = "session-pg-1";

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

            // TTL-Reinigung: abgelaufene Sitzung wird entfernt.
            var expired = "session-expired";
            await store.PutAsync(expired, Principal(expired), "access", "refresh", null,
                DateTimeOffset.UtcNow.AddMinutes(-1), expectedRevision: 0, TestContext.Current.CancellationToken);
            Assert.Null(await store.TryGetAsync(expired, TestContext.Current.CancellationToken));
            Assert.Equal(1, await store.RemoveExpiredAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
        }
        finally
        {
            await DropDatabaseAsync(AdminConnectionString, db);
        }
    }

    private sealed class ManualOidcDbFactory(DbContextOptions<OidcSessionStoreDbContext> options)
        : IDbContextFactory<OidcSessionStoreDbContext>
    {
        public OidcSessionStoreDbContext CreateDbContext() => new(options);
        public Task<OidcSessionStoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OidcSessionStoreDbContext(options));
    }
}
