using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersistentCredentialServiceTests
{
    [Fact]
    public async Task CreateAndResolve_EncryptsValuesAndReturnsMetadataOnly()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new BpmnDbContext(options);
        var audit = new Mock<IAuditLogService>();
        var service = CreateService(db, audit);

        var metadata = await service.CreateAsync("tenant-a", new CredentialWriteRequest(
            "Payments", "api-key", "Payment gateway", new Dictionary<string, string> { ["token"] = "super-secret-value" }));

        Assert.DoesNotContain("super-secret-value", metadata.ToString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "token" }, metadata.SecretKeys);

        var stored = await db.Credentials.SingleAsync();
        Assert.DoesNotContain("super-secret-value", stored.ProtectedValues, StringComparison.Ordinal);
        Assert.Equal("super-secret-value", await service.ResolveSecretAsync("tenant-a", metadata.Id, "token"));
        audit.Verify(value => value.RecordAsync(It.Is<AuditLog>(log => log.Action == "credential.created"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAndResolve_AreTenantIsolated()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new BpmnDbContext(options);
        var service = CreateService(db, new Mock<IAuditLogService>());
        var created = await service.CreateAsync("tenant-a", new CredentialWriteRequest(
            "Only A", "password", null, new Dictionary<string, string> { ["password"] = "a-secret" }));

        Assert.Single(await service.ListAsync("tenant-a"));
        Assert.Empty(await service.ListAsync("tenant-b"));
        Assert.Null(await service.GetAsync("tenant-b", created.Id));
        Assert.Null(await service.ResolveSecretAsync("tenant-b", created.Id, "password"));
        Assert.False(await service.DeleteAsync("tenant-b", created.Id));
        Assert.True(await service.DeleteAsync("tenant-a", created.Id));
    }

    private static PersistentCredentialService CreateService(BpmnDbContext db, Mock<IAuditLogService> audit) =>
        new(db, DataProtectionProvider.Create("VertexBPMN.Tests"), audit.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<PersistentCredentialService>>());
}
