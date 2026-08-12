using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersistentConnectorServiceTests
{
    [Fact]
    public async Task CreateAndList_StayTenantScopedAndReferenceCredentialById()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new BpmnDbContext(options);
        var credential = new Mock<ICredentialService>();
        credential.Setup(value => value.GetAsync("tenant-a", "credential-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CredentialMetadata("credential-1", "tenant-a", "Payments", "api-key", null, ["token"], DateTime.UtcNow, DateTime.UtcNow, null));
        var audit = new Mock<IAuditLogService>();
        var service = new PersistentConnectorService(db, credential.Object, audit.Object);

        var created = await service.CreateAsync("tenant-a", new ConnectorWriteRequest(
            "Payments", "http", "Payment gateway", "https://payments.example.test", "credential-1"));

        Assert.Equal("credential-1", created.CredentialId);
        Assert.Single(await service.ListAsync("tenant-a"));
        Assert.Empty(await service.ListAsync("tenant-b"));
        Assert.Null(await service.GetAsync("tenant-b", created.Id));
        audit.Verify(value => value.RecordAsync(It.Is<AuditLog>(log => log.Action == "connector.created" && log.TenantId == "tenant-a"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_RejectsCrossTenantCredentialAndNonHttpEndpoint()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new BpmnDbContext(options);
        var credential = new Mock<ICredentialService>();
        credential.Setup(value => value.GetAsync("tenant-a", "credential-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CredentialMetadata?)null);
        var service = new PersistentConnectorService(db, credential.Object, Mock.Of<IAuditLogService>());

        await Assert.ThrowsAsync<ConnectorCredentialException>(() => service.CreateAsync("tenant-a",
            new ConnectorWriteRequest("Payments", "http", null, "https://payments.example.test", "credential-1")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("tenant-a",
            new ConnectorWriteRequest("Payments", "http", null, "file:///tmp/secret", null)));
    }
}
