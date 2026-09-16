using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class ExternalTaskOperationsControllerTests
{
    [Fact]
    public void ControllerRequiresTenantReadOnlyPolicy()
    {
        var authorize = Assert.Single(typeof(ExternalTaskOperationsController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal("TenantReadOnly", authorize.Policy);
    }

    [Fact]
    public void CatalogReturnsOnlyCurrentTenantAndNoSensitiveConfiguration()
    {
        using var db = Database();
        var configuration = Configuration();
        var controller = Controller(db, configuration, Principal("tenant-a", "ReadOnly"));

        var result = controller.Catalog("tenant-a");

        var profile = Assert.Single(Assert.IsType<OkObjectResult>(result.Result).Value as IReadOnlyList<ExternalTaskOperationsController.ExternalTaskProfileDto>
            ?? throw new Xunit.Sdk.XunitException("Expected profile list."));
        Assert.Equal("contract-reviewer.v1", profile.ProfileRef);
        Assert.Equal("agent.contract-review", profile.Topic);
        Assert.DoesNotContain("secret", System.Text.Json.JsonSerializer.Serialize(profile), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogForbidsCrossTenantRequest()
    {
        using var db = Database();
        var result = Controller(db, Configuration(), Principal("tenant-a", "ReadOnly")).Catalog("tenant-b");
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public void CatalogIsEmptyWhenExternalTaskSchedulingIsDisabled()
    {
        using var db = Database();
        var configuration = Configuration();
        configuration["ExternalTasks:EnableSchedulingPreview"] = "false";

        var result = Controller(db, configuration, Principal("tenant-a", "ReadOnly")).Catalog("tenant-a");

        Assert.Empty(Assert.IsType<OkObjectResult>(result.Result).Value as IReadOnlyList<ExternalTaskOperationsController.ExternalTaskProfileDto>
            ?? throw new Xunit.Sdk.XunitException("Expected profile list."));
    }

    [Fact]
    public async Task ProcessQueryIsTenantScopedAndOmitsPayloadAndWorkerIdentity()
    {
        await using var db = Database();
        var processId = Guid.NewGuid();
        db.ProcessInstances.Add(new ProcessInstance { Id = processId, TenantId = "tenant-a", State = "Waiting" });
        db.ExternalTaskJobs.Add(new ExternalTaskJob
        {
            Id = Guid.NewGuid(), TenantId = "tenant-a", ProcessInstanceId = processId, ActivityId = "review",
            Topic = "agent.contract-review", AgentProfileRef = "contract-reviewer.v1", AgentProfileVersion = "v1",
            State = ExternalTaskState.Leased, CreatedAt = 1, AvailableAt = 1, Deadline = 1000,
            AttemptsStarted = 1, MaxAttempts = 2, InputSnapshot = "{\"secret\":\"must-not-leak\"}",
            WorkerIssuer = "issuer", WorkerSubject = "worker"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Controller(db, Configuration(), Principal("tenant-a", "ReadOnly"))
            .Process(processId, "tenant-a", null, TestContext.Current.CancellationToken);

        var operation = Assert.Single(Assert.IsType<OkObjectResult>(result.Result).Value as IReadOnlyList<ExternalTaskOperationsController.ExternalTaskOperationDto>
            ?? throw new Xunit.Sdk.XunitException("Expected operation list."));
        var json = System.Text.Json.JsonSerializer.Serialize(operation);
        Assert.Equal("review", operation.ActivityId);
        Assert.DoesNotContain("must-not-leak", json);
        Assert.DoesNotContain("worker", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leaseId", json, StringComparison.OrdinalIgnoreCase);
    }

    private static ExternalTaskOperationsController Controller(BpmnDbContext db, IConfiguration configuration, ClaimsPrincipal user) =>
        new(db, configuration) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };

    private static ClaimsPrincipal Principal(string tenant, string role) => new(new ClaimsIdentity(
        [new Claim("tenant_id", tenant), new Claim(ClaimTypes.Role, role)], "test", ClaimTypes.Name, ClaimTypes.Role));

    private static BpmnDbContext Database() => new(new DbContextOptionsBuilder<BpmnDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ExternalTasks:EnableSchedulingPreview"] = "true",
        ["ExternalTasks:Contracts:0:TenantId"] = "tenant-a", ["ExternalTasks:Contracts:0:Enabled"] = "true",
        ["ExternalTasks:Contracts:0:Topic"] = "agent.contract-review", ["ExternalTasks:Contracts:0:Version"] = "v1",
        ["ExternalTasks:Contracts:0:AgentProfileRef"] = "contract-reviewer.v1",
        ["ExternalTasks:Contracts:0:AgentProfileVersion"] = "contract-reviewer.v1",
        ["ExternalTasks:Contracts:0:MaxAttempts"] = "2", ["ExternalTasks:Contracts:0:MaxDeadlineSeconds"] = "300",
        ["ExternalTasks:Contracts:0:Inputs:0:Name"] = "document", ["ExternalTasks:Contracts:0:Inputs:0:Type"] = "string",
        ["ExternalTasks:Contracts:0:Inputs:0:Required"] = "true", ["ExternalTasks:Contracts:0:Inputs:0:MaxLength"] = "65536",
        ["ExternalTasks:Contracts:0:Inputs:0:AllowExternalTransfer"] = "true",
        ["ExternalTasks:Contracts:1:TenantId"] = "tenant-b", ["ExternalTasks:Contracts:1:Enabled"] = "true",
        ["ExternalTasks:Contracts:1:Topic"] = "agent.other", ["ExternalTasks:Contracts:1:Version"] = "v1",
        ["ExternalTasks:Contracts:1:AgentProfileRef"] = "other.v1"
    }).Build();
}
