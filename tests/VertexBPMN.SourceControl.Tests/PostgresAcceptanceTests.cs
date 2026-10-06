using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class PostgresAcceptanceTests
{
    [Fact]
    public Task Real_postgres_claimed_runner_recovers_git_effect_and_durable_error_state()
        => CommitExecutionAcceptanceTests.RunClaimedRunnerRecoveryAsync(true);

    [Fact]
    public Task Real_postgres_reconciles_git_effect_after_failed_database_finish()
        => CommitExecutionAcceptanceTests.RunRecoveryAsync(true);

    [Fact]
    public Task Real_postgres_migration_idempotency_and_fenced_recovery() => RunAsync(false);

    [Fact]
    public Task Real_wslc_database_restart_preserves_accepted_operation() => RunAsync(true);

    private static async Task RunAsync(bool restart)
    {
        var connection = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Explicit isolated PostgreSQL test connection required.");
        var database = "vertex_git_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connection) { Database = database, Pooling = false };
            // Use the same shared-migration provider configuration as production.
            BpmnDbContext Db() => new(new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(builder.ConnectionString).Options);
            var protection = new EphemeralDataProtectionProvider();
            var options = Options.Create(new SourceControlOptions { Enabled = true, AllowedHosts = ["github.com"] });
            PersistentSourceControlStore Store(BpmnDbContext db) => new(db, protection, options);
            var actor = new SourceControlContext("test-tenant", "test-actor");
            var binding = new RepositoryBinding(Guid.NewGuid(), actor.TenantId, new Uri("https://github.com/example/models.git"), null, "master", "release", ["models"]);
            Guid operation;
            await using (var db = Db())
            {
                await db.GetService<IMigrator>().MigrateAsync("20260912090238_ExternalTaskPersistence", TestContext.Current.CancellationToken);
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                await Store(db).CreateBindingAsync(actor, ["Admin"], binding, TestContext.Current.CancellationToken);
                Assert.True(await Store(db).ReplaceGrantsAsync(actor, binding.Id, ["Admin"], 1,
                    [new(actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Push)], TestContext.Current.CancellationToken));
                operation = await Store(db).EnqueueAsync(actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
                    new("postgres-key"), new byte[] { 1 }, TestContext.Current.CancellationToken);
            }
            if (restart)
            {
                var executable = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_WSLC");
                var container = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_CONTAINER");
                Assert.True(executable is not null && Path.IsPathFullyQualified(executable) && File.Exists(executable));
                Assert.True(container is not null && container.StartsWith("vertex-source-control-phase2-", StringComparison.Ordinal)
                    && container.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'), "Restart is restricted to the isolated acceptance container.");
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("restart");
                start.ArgumentList.Add(container);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                _ = await stdout;
                _ = await stderr;
                Assert.Equal(0, process.ExitCode);
                var ready = false;
                for (var attempt = 0; attempt < 100 && !ready; attempt++)
                {
                    try
                    {
                        await using var probe = new NpgsqlConnection(builder.ConnectionString);
                        await probe.OpenAsync(timeout.Token);
                        ready = true;
                    }
                    catch (NpgsqlException) { await Task.Delay(100, timeout.Token); }
                }
                Assert.True(ready, "PostgreSQL must become ready after the real WSLC restart.");
            }
            await using var first = Db();
            await using var second = Db();
            Assert.Equal(operation, await Store(second).EnqueueAsync(actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
                new("postgres-key"), new byte[] { 1 }, TestContext.Current.CancellationToken));
            var now = DateTimeOffset.UtcNow;
            var claims = await Task.WhenAll(Store(first).TryClaimAsync(actor.TenantId, operation, "first", now, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
                Store(second).TryClaimAsync(actor.TenantId, operation, "second", now, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Single(claims, x => x.HasValue);
            await using var restarted = Db();
            Assert.Equal(2L, await Store(restarted).TryClaimAsync(actor.TenantId, operation, "recovery", now.AddSeconds(6), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(SourceControlOperationState.Reconciling, (await Store(restarted).GetOperationAsync(actor, operation, ["Admin"], TestContext.Current.CancellationToken))!.State);
            Assert.False(await Store(first).FinishAsync(actor.TenantId, operation, "first", 1, SourceControlOperationState.Pushed, now.AddSeconds(7), TestContext.Current.CancellationToken));
        }
        finally
        {
            if (restart) { await admin.CloseAsync(); await admin.OpenAsync(CancellationToken.None); }
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
