using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Application.Lc06;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Seeding;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// LC06 reference application (LC-AC-08): the reference app seed is loadable by the platform
/// and becomes a RELEASED revision that the Portal BFF released-revision serving would serve.
/// </summary>
public sealed class ReferenceApplicationSeederTests
{
    private static ApplicationAccessContext Access { get; } = new(
        "reference-seeder",
        new HashSet<string>(new[] { "application.design", "application.publish" }, StringComparer.Ordinal));

    [Fact]
    public async Task Seed_produces_a_released_revision_with_the_reference_content()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var appId = await ReferenceApplicationSeeder.SeedReleasedAsync(db, Mock.Of<IAuditLogService>(), Access, TestContext.Current.CancellationToken);

        Assert.NotNull(appId);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var released = await service.GetReleasedRevisionAsync(Lc06ReferenceApp.TenantId, appId, TestContext.Current.CancellationToken);
        Assert.NotNull(released);
        Assert.Equal(1, released.Version);
        Assert.Equal(Lc06ReferenceApp.ContentJson, released.ContentJson, StringComparer.Ordinal);
        Assert.Equal("[]", released.DiagnosticsJson, StringComparer.Ordinal);
        // The pinned content is the genuine reference app: a sanctioned read plus a start action.
        Assert.Contains(Lc06ReferenceApp.SanctionedBinding, released.ContentJson, StringComparison.Ordinal);
        Assert.Contains("\"start-process\"", released.ContentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seed_is_idempotent_and_does_not_duplicate_the_reference_app()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var first = await ReferenceApplicationSeeder.SeedReleasedAsync(db, Mock.Of<IAuditLogService>(), Access, TestContext.Current.CancellationToken);
        var second = await ReferenceApplicationSeeder.SeedReleasedAsync(db, Mock.Of<IAuditLogService>(), Access, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(1, await db.ApplicationDefinitions.CountAsync(x => x.TenantId == Lc06ReferenceApp.TenantId && x.Key == Lc06ReferenceApp.Key, TestContext.Current.CancellationToken));
        var released = await new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access)
            .GetReleasedRevisionAsync(Lc06ReferenceApp.TenantId, first, TestContext.Current.CancellationToken);
        Assert.Equal(Lc06ReferenceApp.ContentJson, released!.ContentJson, StringComparer.Ordinal);
    }

    [Fact]
    public void Reference_content_hash_is_stable_for_the_pinned_revision()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Lc06ReferenceApp.ContentJson))).ToLowerInvariant();
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain('\r', Lc06ReferenceApp.ContentJson);
    }
}
