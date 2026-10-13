using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class SourceControlConnectionProfilePersistenceTests
{
	private static readonly SourceControlContext Context = new("tenant-a", "administrator");
	private static readonly string[] Admin = ["Admin"];

	[Fact]
	public async Task Profile_survives_restart_and_tenants_can_reuse_the_same_identifier()
	{
		await using var connection = await OpenAsync();
		var profile = Profile();
		await using (var db = Database(connection))
		{
			await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
			await Store(db).CreateConnectionProfileAsync(Context, Admin, profile, TestContext.Current.CancellationToken);
			var foreign = new SourceControlContext("tenant-b", "administrator");
			Assert.Null(await Store(db).FindConnectionProfileAsync(foreign, Admin, profile.Id, TestContext.Current.CancellationToken));
			await Store(db).CreateConnectionProfileAsync(foreign, Admin, profile with { TenantId = foreign.TenantId },
				TestContext.Current.CancellationToken);
		}

		await using var restarted = Database(connection);
		Assert.Equal(profile, await Store(restarted).FindConnectionProfileAsync(Context, Admin, profile.Id,
			TestContext.Current.CancellationToken));
		Assert.Equal(2, await restarted.SourceControlConnectionProfiles.CountAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Separate_scopes_cannot_replace_an_already_changed_revision()
	{
		await using var connection = await OpenAsync();
		await using var first = Database(connection);
		await first.Database.MigrateAsync(TestContext.Current.CancellationToken);
		var profile = Profile();
		await Store(first).CreateConnectionProfileAsync(Context, Admin, profile, TestContext.Current.CancellationToken);
		await using var second = Database(connection);
		var competing = await Store(second).FindConnectionProfileAsync(Context, Admin, profile.Id,
			TestContext.Current.CancellationToken);
		Assert.NotNull(competing);
		var changed = profile with { Revision = 2, GitCredentialReference = "rotated-credential" };
		Assert.True(await Store(first).ReplaceConnectionProfileAsync(Context, Admin, 1, changed,
			TestContext.Current.CancellationToken));
		Assert.False(await Store(second).ReplaceConnectionProfileAsync(Context, Admin, 1,
			competing with { Revision = 2, GitCredentialReference = "stale-credential" }, TestContext.Current.CancellationToken));
		Assert.Equal(changed, await Store(second).FindConnectionProfileAsync(Context, Admin, profile.Id,
			TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Revocation_is_revisioned_preserves_metadata_and_cannot_be_reactivated()
	{
		await using var connection = await OpenAsync();
		await using var db = Database(connection);
		await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
		var profile = Profile();
		var store = Store(db);
		await store.CreateConnectionProfileAsync(Context, Admin, profile, TestContext.Current.CancellationToken);
		Assert.False(await store.RevokeConnectionProfileAsync(Context, Admin, profile.Id, 2, TestContext.Current.CancellationToken));
		Assert.True(await store.RevokeConnectionProfileAsync(Context, Admin, profile.Id, 1, TestContext.Current.CancellationToken));
		Assert.Null(await store.FindConnectionProfileAsync(Context, Admin, profile.Id, TestContext.Current.CancellationToken));
		Assert.False(await store.ReplaceConnectionProfileAsync(Context, Admin, 2, profile with { Revision = 3 },
			TestContext.Current.CancellationToken));
		Assert.False(await store.RevokeConnectionProfileAsync(Context, Admin, profile.Id, 2, TestContext.Current.CancellationToken));
		var retained = await db.SourceControlConnectionProfiles.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
		Assert.True(retained.IsRevoked);
		Assert.Equal(2, retained.Revision);
		Assert.Equal(profile with { Revision = 2 }, JsonSerializer.Deserialize<RepositoryConnectionProfile>(retained.ProfileJson));
		await RejectAsync(SourceControlErrorCode.RevisionConflict,
			() => store.CreateConnectionProfileAsync(Context, Admin, profile, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Administration_requires_exact_role_and_matching_tenant()
	{
		await using var connection = await OpenAsync();
		await using var db = Database(connection);
		await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
		var profile = Profile();
		var store = Store(db);
		await RejectAsync(SourceControlErrorCode.Forbidden,
			() => store.CreateConnectionProfileAsync(Context, ["admin"], profile, TestContext.Current.CancellationToken));
		await RejectAsync(SourceControlErrorCode.Forbidden,
			() => store.CreateConnectionProfileAsync(Context, Admin, profile with { TenantId = "tenant-b" },
				TestContext.Current.CancellationToken));
		await store.CreateConnectionProfileAsync(Context, Admin, profile, TestContext.Current.CancellationToken);
		await RejectAsync(SourceControlErrorCode.Forbidden,
			() => store.FindConnectionProfileAsync(Context, [], profile.Id, TestContext.Current.CancellationToken));
		await RejectAsync(SourceControlErrorCode.Forbidden,
			() => store.ReplaceConnectionProfileAsync(Context, [], 1, profile with { Revision = 2 },
				TestContext.Current.CancellationToken));
		await RejectAsync(SourceControlErrorCode.Forbidden,
			() => store.RevokeConnectionProfileAsync(Context, [], profile.Id, 1, TestContext.Current.CancellationToken));
		var foreign = new SourceControlContext("tenant-b", "administrator");
		Assert.False(await store.ReplaceConnectionProfileAsync(foreign, Admin, 1,
			profile with { TenantId = foreign.TenantId, Revision = 2 }, TestContext.Current.CancellationToken));
		Assert.False(await store.RevokeConnectionProfileAsync(foreign, Admin, profile.Id, 1, TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(long.MaxValue)]
	public async Task Invalid_expected_revisions_are_rejected(long revision)
	{
		await using var connection = await OpenAsync();
		await using var db = Database(connection);
		await RejectAsync(SourceControlErrorCode.InvalidInput,
			() => Store(db).RevokeConnectionProfileAsync(Context, Admin, Guid.NewGuid(), revision,
				TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("{broken")]
	[InlineData("identity")]
	[InlineData("revision")]
	[InlineData("schema")]
	public async Task Corrupted_persisted_metadata_is_rejected_with_safe_error(string corruption)
	{
		await using var connection = await OpenAsync();
		await using var db = Database(connection);
		await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
		var profile = Profile();
		var json = corruption switch
		{
			"identity" => JsonSerializer.Serialize(profile with { Id = Guid.NewGuid() }),
			"revision" => JsonSerializer.Serialize(profile with { Revision = 2 }),
			"schema" => JsonSerializer.Serialize(profile with { SchemaVersion = 99 }),
			_ => corruption
		};
		db.SourceControlConnectionProfiles.Add(new()
		{
			Id = profile.Id,
			TenantId = Context.TenantId,
			Revision = 1,
			ProfileJson = json
		});
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);
		await RejectAsync(SourceControlErrorCode.ContentUnsafe,
			() => Store(db).FindConnectionProfileAsync(Context, Admin, profile.Id,
				TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Invalid_creation_revision_and_oversized_metadata_are_rejected()
	{
		await using var connection = await OpenAsync();
		await using var db = Database(connection);
		await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
		var profile = Profile();
		await RejectAsync(SourceControlErrorCode.InvalidInput,
			() => Store(db).CreateConnectionProfileAsync(Context, Admin, profile with { Revision = 2 },
				TestContext.Current.CancellationToken));
		var oversized = profile with
		{
			GitEndpoint = profile.GitEndpoint with { BaseUri = new Uri("https://git.example.com/" + new string('a', 40000)) }
		};
		await RejectAsync(SourceControlErrorCode.PayloadTooLarge,
			() => Store(db).CreateConnectionProfileAsync(Context, Admin, oversized, TestContext.Current.CancellationToken));
		Assert.Empty(await db.SourceControlConnectionProfiles.ToListAsync(TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData("postgres", "uuid", "boolean", "character varying(64)")]
	[InlineData("sqlserver", "uniqueidentifier", "bit", "nvarchar(64)")]
	public void Migration_generates_provider_specific_sql_without_connecting(string provider,
		string identifierType, string booleanType, string tenantType)
	{
		var options = new DbContextOptionsBuilder<BpmnDbContext>();
		if (provider == "postgres")
		{
			options.UseNpgsql("Host=localhost;Database=script-only;Username=script-only");
		}
		else
		{
			options.UseSqlServer("Server=localhost;Database=script-only;Integrated Security=true;TrustServerCertificate=true");
		}

		using var db = new BpmnDbContext(options.Options);
		var sql = db.GetService<IMigrator>().GenerateScript("20261005131442_SourceControlPersistence",
			"20261011134324_SourceControlConnectionProfiles");
		Assert.Contains("SourceControlConnectionProfiles", sql, StringComparison.Ordinal);
		Assert.Contains(identifierType, sql, StringComparison.Ordinal);
		Assert.Contains(booleanType, sql, StringComparison.Ordinal);
		Assert.Contains(tenantType, sql, StringComparison.Ordinal);
		Assert.DoesNotContain("SourceControlBindings", sql, StringComparison.Ordinal);
		Assert.DoesNotContain("SourceControlOperations", sql, StringComparison.Ordinal);
		Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
		var contractSql = db.GetService<IMigrator>().GenerateScript("20261011134324_SourceControlConnectionProfiles",
			"20261011140107_SourceControlExecutionContracts");
		Assert.Contains("ExecutionContractVersion", contractSql, StringComparison.Ordinal);
		Assert.Contains("ProtectedExecutionTarget", contractSql, StringComparison.Ordinal);
		Assert.Contains("DEFAULT 0", contractSql, StringComparison.Ordinal);
		Assert.DoesNotContain("DROP ", contractSql, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("UPDATE ", contractSql, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task Disabled_store_and_cancelled_io_do_not_save_profiles()
	{
		await using var connection = await OpenAsync();
		await using var db = Database(connection);
		await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
		var disabled = new PersistentSourceControlStore(db, new EphemeralDataProtectionProvider(),
			Options.Create(new SourceControlOptions { Enabled = false }));
		await RejectAsync(SourceControlErrorCode.Disabled,
			() => disabled.CreateConnectionProfileAsync(Context, Admin, Profile(), TestContext.Current.CancellationToken));
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(db).CreateConnectionProfileAsync(
			Context, Admin, Profile(), cancellation.Token));
		Assert.Empty(await db.SourceControlConnectionProfiles.ToListAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Schema_upgrade_preserves_existing_bindings_sessions_and_operation_receipts()
	{
		await using var connection = await OpenAsync();
		var bindingId = Guid.NewGuid();
		var operationId = Guid.NewGuid();
		var sessionId = Guid.NewGuid();
		await SeedLegacyAsync(connection, bindingId, operationId, sessionId);

		await using var upgraded = Database(connection);
		await upgraded.Database.MigrateAsync(TestContext.Current.CancellationToken);
		Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
		var binding = await upgraded.SourceControlBindings.SingleAsync(TestContext.Current.CancellationToken);
		Assert.Equal(bindingId, binding.Id);
		Assert.Equal(7, binding.Revision);
		Assert.Equal("legacy-binding-unchanged", binding.BindingJson);
		Assert.Equal("legacy-grants-unchanged", binding.GrantsJson);
		var session = await upgraded.SourceControlSessions.SingleAsync(TestContext.Current.CancellationToken);
		Assert.Equal(sessionId, session.Id);
		Assert.Equal(4, session.Revision);
		Assert.Equal("legacy-protected-snapshots", session.ProtectedSnapshots);
		var operation = await upgraded.SourceControlOperations.SingleAsync(TestContext.Current.CancellationToken);
		Assert.Equal(operationId, operation.Id);
		Assert.Equal(9, operation.Fence);
		Assert.Equal("legacy-worker", operation.LeaseOwner);
		Assert.Equal(12345, operation.LeaseUntilUtcTicks);
		Assert.Equal("legacy-protected-request", operation.ProtectedRequest);
		Assert.Equal("legacy-protected-result", operation.ProtectedResult);
		Assert.Equal(0, operation.ExecutionContractVersion);
		Assert.Null(operation.ProtectedExecutionTarget);
		Assert.Equal(2, operation.State);
		Assert.Empty(await upgraded.SourceControlConnectionProfiles.ToListAsync(TestContext.Current.CancellationToken));
		await Store(upgraded).CreateConnectionProfileAsync(Context, Admin, Profile(), TestContext.Current.CancellationToken);
	}

	private static async Task SeedLegacyAsync(SqliteConnection connection, Guid bindingId, Guid operationId, Guid sessionId)
	{
		await using var legacy = Database(connection);
		await legacy.GetService<IMigrator>().MigrateAsync("20261005131442_SourceControlPersistence",
			TestContext.Current.CancellationToken);
		legacy.SourceControlBindings.Add(new()
		{
			Id = bindingId,
			TenantId = Context.TenantId,
			Revision = 7,
			BindingJson = "legacy-binding-unchanged",
			GrantsJson = "legacy-grants-unchanged"
		});
		legacy.SourceControlSessions.Add(new()
		{
			Id = sessionId,
			RepositoryId = bindingId,
			TenantId = Context.TenantId,
			ActorId = Context.ActorId,
			Revision = 4,
			BaseCommit = new string('a', 40),
			ProtectedSnapshots = "legacy-protected-snapshots"
		});
		await legacy.SaveChangesAsync(TestContext.Current.CancellationToken);
		// Use only the columns of the old schema, before the new model's fields exist.
		await legacy.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO "SourceControlOperations"
			("Id", "RepositoryId", "TenantId", "ActorId", "Kind", "IdempotencyKey", "RequestHash",
			 "ProtectedRequest", "ProtectedResult", "State", "UpdatedUtcTicks", "Fence", "LeaseOwner", "LeaseUntilUtcTicks")
			VALUES ({operationId}, {bindingId}, {Context.TenantId}, {Context.ActorId}, 1, 'legacy-operation', {new string('b', 64)},
			 'legacy-protected-request', 'legacy-protected-result', 2, 0, 9, 'legacy-worker', 12345)
			""", TestContext.Current.CancellationToken);
	}

	private static RepositoryConnectionProfile Profile() => new(Guid.NewGuid(), Context.TenantId, 1,
		GitTransportKind.Https, GitAuthenticationKind.HttpBasic,
		new(new Uri("https://git.example.com:8443/team"), "corporate-network", "corporate-ca"),
		"credential-reference", SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push);

	private static async Task<SqliteConnection> OpenAsync()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		return connection;
	}

	private static BpmnDbContext Database(SqliteConnection connection) => new(
		new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);

	private static PersistentSourceControlStore Store(BpmnDbContext db) => new(db,
		new EphemeralDataProtectionProvider(), Options.Create(new SourceControlOptions { Enabled = true }));

	private static async Task RejectAsync(SourceControlErrorCode code, Func<Task> action)
	{
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(action);
		Assert.Equal(code, error.Code);
	}
}
