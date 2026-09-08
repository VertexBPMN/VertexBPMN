using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Domain.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace VertexBPMN.Tests.Acceptance;

public sealed class OAuth2PostgresMigrationTests
{
    private const string Legacy = "20260906175904_AddOAuth2FlowStates";

    [Fact]
    public async Task SqliteUpgrade_PreservesStateAndCleanup()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync(Legacy, TestContext.Current.CancellationToken);
        db.OAuth2FlowStates.Add(new OAuth2FlowStateRecord { State = "expired", CreatedAt = DateTime.UtcNow.AddHours(-2), ExpiresAt = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await db.OAuth2FlowStates.Where(s => s.ExpiresAt <= DateTime.UtcNow).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("Category", "Phase3ExternalAcceptance")]
    public async Task FreshAndUpgradedDatabase_PreserveUtcStatesAndDeleteOnlyExpired(bool upgrade, bool invalidLegacy)
    {
        var adminString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminString), "Set VERTEXBPMN_TEST_POSTGRES_ADMIN for local PostgreSQL acceptance.");
        var database = $"oauth_migration_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminString)
            { Database = database, Timezone = "Europe/Berlin", Pooling = false };
            await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
                .UseVertexNpgsql(connection.ConnectionString).Options);
            if (upgrade)
            {
                await db.GetService<IMigrator>().MigrateAsync(Legacy, TestContext.Current.CancellationToken);
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "OAuth2FlowStates"
                    ("State", "TenantId", "CredentialId", "AuthorizationUrl", "TokenUrl", "ClientId", "RedirectUri", "Scopes", "CreatedAt", "ExpiresAt")
                    VALUES ('legacy', 'tenant-a', 'credential', '', '', '', '', '', '2026-09-08 12:00:00', '2099-01-01T02:00:00+02:00');
                    """, TestContext.Current.CancellationToken);
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.OAuth2FlowStates
                    .Where(s => s.ExpiresAt <= DateTime.UtcNow).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
                Assert.Equal(PostgresErrorCodes.UndefinedFunction, error.SqlState);
                if (invalidLegacy)
                {
                    await db.Database.ExecuteSqlRawAsync("""UPDATE "OAuth2FlowStates" SET "ExpiresAt" = 'invalid-date' WHERE "State" = 'legacy'""", TestContext.Current.CancellationToken);
                    var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync(TestContext.Current.CancellationToken));
                    Assert.Equal(PostgresErrorCodes.InvalidDatetimeFormat, invalid.SqlState);
                    Assert.DoesNotContain("20260908090000_NormalizePostgresOAuth2FlowStateTimes",
                        await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
                    // Failed conversion must retain both the row and original text types.
                    Assert.Equal("invalid-date", await db.Database.SqlQueryRaw<string>("""SELECT "ExpiresAt" AS "Value" FROM "OAuth2FlowStates" WHERE "State" = 'legacy'""").SingleAsync(TestContext.Current.CancellationToken));
                    Assert.Equal("text", await db.Database.SqlQueryRaw<string>("""SELECT pg_typeof("CreatedAt")::text AS "Value" FROM "OAuth2FlowStates" WHERE "State" = 'legacy'""").SingleAsync(TestContext.Current.CancellationToken));
                    await db.Database.ExecuteSqlRawAsync("""UPDATE "OAuth2FlowStates" SET "ExpiresAt" = '2099-01-01T02:00:00+02:00' WHERE "State" = 'legacy'""", TestContext.Current.CancellationToken);
                }
            }

            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            if (upgrade)
            {
                var legacy = await db.OAuth2FlowStates.SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc), legacy.CreatedAt);
                Assert.Equal(new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc), legacy.ExpiresAt);
                Assert.Equal(DateTimeKind.Utc, legacy.ExpiresAt.Kind);
            }

            db.OAuth2FlowStates.AddRange(
                new OAuth2FlowStateRecord { State = "expired", TenantId = "tenant-a", CreatedAt = DateTime.UtcNow.AddHours(-2), ExpiresAt = DateTime.UtcNow.AddHours(-1) },
                new OAuth2FlowStateRecord { State = "active", TenantId = "tenant-b", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1) });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, await db.OAuth2FlowStates.Where(s => s.ExpiresAt <= DateTime.UtcNow)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
            db.ChangeTracker.Clear();
            Assert.True(await db.OAuth2FlowStates.AnyAsync(s => s.State == "active", TestContext.Current.CancellationToken));
            Assert.False(await db.OAuth2FlowStates.AnyAsync(s => s.State == "expired", TestContext.Current.CancellationToken));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
            await AssertCallbackLifecycleAsync(db);
        }
        finally
        {
            // Name is generated here, never supplied by an external configuration.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static async Task AssertCallbackLifecycleAsync(BpmnDbContext db)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var credentials = new PersistentCredentialService(db, new EphemeralDataProtectionProvider(),
            Mock.Of<IAuditLogService>(), NullLogger<PersistentCredentialService>.Instance);
        var credential = await credentials.CreateAsync("tenant-a", new CredentialWriteRequest(
            "Postgres OAuth", "oauth2", null, new Dictionary<string, string> { ["client_secret"] = "test-secret" }), cancellation);
        using var handler = new TokenEndpoint();
        using var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var flow = new OAuth2CredentialFlowService(db, credentials, factory.Object,
            Mock.Of<IAuditLogService>(), NullLogger<OAuth2CredentialFlowService>.Instance);
        var config = new OAuth2AuthorizationConfig("https://oauth.test/authorize", "https://oauth.test/token",
            "client", "https://studio.test/callback", "read");
        await Assert.ThrowsAsync<ArgumentException>(() => flow.StartAuthorizationAsync("tenant-b", credential.Id, config, cancellation));
        var started = await flow.StartAuthorizationAsync("tenant-a", credential.Id, config, cancellation);
        db.ChangeTracker.Clear();
        Assert.True(await flow.CompleteAuthorizationAsync(started.State, "test-code", cancellation));
        db.ChangeTracker.Clear();
        Assert.Equal("test-access", await credentials.ResolveSecretAsync("tenant-a", credential.Id, "access_token", cancellation));
        Assert.Null(await credentials.ResolveSecretAsync("tenant-b", credential.Id, "access_token", cancellation));
        Assert.False(await flow.CompleteAuthorizationAsync(started.State, "test-code", cancellation));
        Assert.False(await flow.CompleteAuthorizationAsync("unknown", "test-code", cancellation));
        var expired = await flow.StartAuthorizationAsync("tenant-a", credential.Id, config, cancellation);
        var record = await db.OAuth2FlowStates.SingleAsync(s => s.State == expired.State, cancellation);
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(cancellation);
        db.ChangeTracker.Clear();
        Assert.False(await flow.CompleteAuthorizationAsync(expired.State, "test-code", cancellation));
        Assert.False(await db.OAuth2FlowStates.AnyAsync(s => s.State == expired.State, cancellation));
        Assert.Equal(1, handler.Calls);
    }

    // Only the external identity provider is simulated; state and encrypted secrets use PostgreSQL.
    private sealed class TokenEndpoint : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("https://oauth.test/token", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("grant_type=authorization_code", body);
            Assert.Contains("code=test-code", body);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"test-access","refresh_token":"test-refresh","expires_in":3600}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
