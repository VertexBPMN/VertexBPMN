using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.Stores;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class CmmnHistoryPersistenceTests
{
    [Fact]
    public async Task FeatureFlags_PersistAcrossFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var writeContext = new BpmnDbContext(options))
        {
            await writeContext.Database.EnsureCreatedAsync();
            var flag = await writeContext.FeatureFlags.FindAsync("liveinspector");
            Assert.NotNull(flag);
            flag!.Enabled = false;
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new BpmnDbContext(options);
        var persisted = await readContext.FeatureFlags.FindAsync("liveinspector");

        Assert.NotNull(persisted);
        Assert.False(persisted!.Enabled);
    }

    [Fact]
    public async Task HistoricalCaseData_RoundTripsThroughFreshDbContext()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var timestamp = DateTime.UtcNow;
        var expected = new HistoricalCaseData(
            "case-1",
            new Dictionary<string, object> { ["amount"] = 42 },
            ["plan-1", "plan-2"],
            timestamp);

        await using (var writeContext = new BpmnDbContext(options))
        {
            await new ProductionProcessInstanceStore(writeContext).SaveHistoricalCaseDataAsync(expected);
        }

        await using var readContext = new BpmnDbContext(options);
        var actual = await new ProductionProcessInstanceStore(readContext)
            .GetHistoricalCaseDataAsync("case-1");

        var snapshot = Assert.Single(actual);
        Assert.Equal(expected.CaseId, snapshot.CaseId);
        Assert.Equal(expected.CompletedPlanItems, snapshot.CompletedPlanItems);
        Assert.Equal(expected.Timestamp, snapshot.Timestamp);
        Assert.Equal("42", snapshot.CaseFile["amount"].ToString());
    }

    [Fact]
    public async Task IdentityReadModels_PersistAndResolveMemberships()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var writeContext = new BpmnDbContext(options))
        {
            await writeContext.Database.EnsureCreatedAsync();
            writeContext.IdentityGroups.Add(new IdentityGroupRecord
            {
                Id = "group-1",
                Name = "Operators",
                Type = "role",
                TenantId = "tenant-1"
            });
            writeContext.IdentityGroupMemberships.Add(new IdentityGroupMembershipRecord
            {
                GroupId = "group-1",
                UserId = "1",
                TenantId = "tenant-1"
            });
            writeContext.IdentityAuthorizations.Add(new IdentityAuthorizationRecord
            {
                Id = "authorization-1",
                UserId = "1",
                GroupId = "group-1",
                Resource = "process-definition:SampleProcess",
                Permissions = "read,execute",
                TenantId = "tenant-1"
            });
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new BpmnDbContext(options);
        await using var tenantContext = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var identity = new PersistentIdentityService(readContext, tenantContext);

        var groups = new List<Domain.Interfaces.GroupInfo>();
        await foreach (var group in identity.ListGroupsAsync())
            groups.Add(group);
        var users = new List<Domain.Interfaces.UserInfo>();
        await foreach (var user in identity.ListUsersByGroupAsync("group-1"))
            users.Add(user);
        var authorizations = new List<Domain.Interfaces.AuthorizationInfo>();
        await foreach (var authorization in identity.ListAuthorizationsAsync())
            authorizations.Add(authorization);

        Assert.Collection(groups, group =>
        {
            Assert.Equal("group-1", group.Id);
            Assert.Equal("Operators", group.Name);
        });
        Assert.Collection(users, user => Assert.Equal("admin", user.Username));
        Assert.Collection(authorizations, authorization =>
        {
            Assert.Equal("group-1", authorization.GroupId);
            Assert.Equal("read,execute", authorization.Permissions);
        });

        var tenantGroups = new List<Domain.Interfaces.GroupInfo>();
        await foreach (var group in identity.ListGroupsAsync(tenantId: "tenant-other"))
            tenantGroups.Add(group);
        var tenantAuthorizations = new List<Domain.Interfaces.AuthorizationInfo>();
        await foreach (var authorization in identity.ListAuthorizationsAsync(tenantId: "tenant-other"))
            tenantAuthorizations.Add(authorization);

        Assert.Empty(tenantGroups);
        Assert.Empty(tenantAuthorizations);
    }
}