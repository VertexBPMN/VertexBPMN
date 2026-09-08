using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Api;

/// <summary>
/// Phase 3 (Sicherheitsabnahme) — negative Tenant-/Rollenfälle für die
/// behobenen Cross-Tenant-Lücken (T1 VertexJob, T2 VertexVariable,
/// T3 SimulationScenario, T4 TaskIoSnapshot).
/// </summary>
public sealed class TenantIsolationPhase3SecurityTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    // ---------- T1: VertexJobController ----------

    [Fact]
    public async Task JobGetAll_ForTenantUser_ReturnsOnlyOwnTenantJobs()
    {
        var jobA = Job(TenantA);
        var jobB = Job(TenantB);
        var repo = new Mock<IJobRepository>();
        repo.Setup(x => x.ListDueAsync(It.IsAny<DateTime>())).Returns(Jobs(jobA, jobB));
        var controller = new VertexJobController(repo.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var dtos = new List<JobDto>();
        await foreach (var dto in controller.GetAll())
            dtos.Add(dto);

        var ids = dtos.Select(d => d.Id).ToArray();
        Assert.Contains(jobA.Id.ToString(), ids);
        Assert.DoesNotContain(jobB.Id.ToString(), ids);
    }

    [Fact]
    public async Task JobGetById_ForDifferentTenant_ReturnsNotFound()
    {
        var remote = Job(TenantB);
        var repo = new Mock<IJobRepository>();
        repo.Setup(x => x.GetByIdAsync(remote.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Job?>(remote));
        var controller = new VertexJobController(repo.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.GetById(remote.Id);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task JobGetById_SameTenant_ReturnsDto()
    {
        var own = Job(TenantA);
        var repo = new Mock<IJobRepository>();
        repo.Setup(x => x.GetByIdAsync(own.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Job?>(own));
        var controller = new VertexJobController(repo.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.GetById(own.Id);

        var dto = Assert.IsType<JobDto>(result.Value);
        Assert.Equal(own.Id.ToString(), dto.Id);
    }

    // ---------- T2: VertexVariableController ----------

    [Fact]
    public async Task VariableGet_ForDifferentTenant_ReturnsNotFound_AndDoesNotReadVariables()
    {
        var instance = new ProcessInstance { Id = Guid.NewGuid(), TenantId = TenantB };
        var runtime = new Mock<IRuntimeService>();
        runtime.Setup(x => x.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ProcessInstance?>(instance));
        var controller = new VertexVariableController(runtime.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.GetVariables(instance.Id);

        Assert.IsType<NotFoundResult>(result.Result);
        runtime.Verify(x => x.GetVariablesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VariableGet_SameTenant_ReturnsVariables()
    {
        var instance = new ProcessInstance { Id = Guid.NewGuid(), TenantId = TenantA };
        var runtime = new Mock<IRuntimeService>();
        runtime.Setup(x => x.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ProcessInstance?>(instance));
        runtime.Setup(x => x.GetVariablesAsync(instance.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IDictionary<string, object>?>(new Dictionary<string, object> { ["x"] = 1 }));
        var controller = new VertexVariableController(runtime.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.GetVariables(instance.Id);

        var map = Assert.IsType<Dictionary<string, VariableValueDto>>(result.Value);
        Assert.True(map.ContainsKey("x"));
    }

    // ---------- T3: SimulationScenarioController ----------

    [Fact]
    public async Task ScenarioGetAll_ForNonAdmin_UsesClaimTenantInsteadOfRequestedTenant()
    {
        var service = new Mock<ISimulationScenarioService>();
        service.Setup(x => x.GetAllAsync(TenantA))
            .ReturnsAsync(new[] { Scenario(TenantA) });
        var controller = new SimulationScenarioController(service.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.GetAll("tenant-b");

        Assert.IsType<OkObjectResult>(result.Result);
        service.Verify(x => x.GetAllAsync(TenantA), Times.Once);
        service.Verify(x => x.GetAllAsync("tenant-b"), Times.Never);
    }

    [Fact]
    public async Task ScenarioGetById_ForDifferentTenant_ReturnsNotFound()
    {
        var service = new Mock<ISimulationScenarioService>();
        service.Setup(x => x.GetByIdAsync("sc-b"))
            .ReturnsAsync(Scenario(TenantB));
        var controller = new SimulationScenarioController(service.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.GetById("sc-b");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ScenarioCreate_ForNonAdmin_ForcesClaimTenant()
    {
        SimulationScenario? captured = null;
        var service = new Mock<ISimulationScenarioService>();
        service.Setup(x => x.CreateAsync(It.IsAny<SimulationScenario>()))
            .Callback<SimulationScenario>(s => captured = s)
            .ReturnsAsync(Scenario(TenantA));
        var controller = new SimulationScenarioController(service.Object)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.Create(new SimulationScenarioDto
        {
            Name = "x",
            MaxSteps = 10,
            TenantId = TenantB
        });

        Assert.NotNull(captured);
        Assert.Equal(TenantA, captured!.TenantId);
        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    // ---------- T4: TaskIoSnapshotController ----------

    [Fact]
    public async Task SnapshotList_ForTenantUser_PinsToClaimTenantNotQueryTenant()
    {
        var processId = Guid.NewGuid();
        var element = "task-1";
        using var db = new BpmnDbContext(
            new DbContextOptionsBuilder<BpmnDbContext>().UseInMemoryDatabase($"t4-{Guid.NewGuid()}").Options);
        db.HistoryEvents.AddRange(
            HistoryEvent(processId, element, TenantA, "a-data"),
            HistoryEvent(processId, element, TenantB, "b-data"));
        await db.SaveChangesAsync();
        var controller = new TaskIoSnapshotController(db)
        {
            ControllerContext = ContextFor(TenantA)
        };

        var result = await controller.List(processId, element, "tenant-b");

        var rows = Assert.IsType<List<TaskIoSnapshotController.TaskIoSnapshotDto>>(result.Value);
        Assert.Single(rows);
        Assert.Equal("a-data", rows[0].Data.GetProperty("v").GetString());
    }

    [Fact]
    public async Task SnapshotList_ForAdmin_RespectsQueryTenant()
    {
        var processId = Guid.NewGuid();
        var element = "task-1";
        using var db = new BpmnDbContext(
            new DbContextOptionsBuilder<BpmnDbContext>().UseInMemoryDatabase($"t4-{Guid.NewGuid()}").Options);
        db.HistoryEvents.AddRange(
            HistoryEvent(processId, element, TenantA, "a-data"),
            HistoryEvent(processId, element, TenantB, "b-data"));
        await db.SaveChangesAsync();
        var controller = new TaskIoSnapshotController(db)
        {
            ControllerContext = ContextFor(TenantA, isAdmin: true)
        };

        var result = await controller.List(processId, element, "tenant-b");

        var rows = Assert.IsType<List<TaskIoSnapshotController.TaskIoSnapshotDto>>(result.Value);
        Assert.Single(rows);
        Assert.Equal("b-data", rows[0].Data.GetProperty("v").GetString());
    }

    // ---------- Helpers ----------

    private static Job Job(string tenantId) => new()
    {
        Id = Guid.NewGuid(),
        ProcessInstanceId = Guid.NewGuid(),
        Type = "timer",
        DueDate = DateTime.UtcNow,
        Retries = 0,
        ErrorMessage = null,
        TenantId = tenantId
    };

    private static SimulationScenario Scenario(string tenantId) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = "s",
        TenantId = tenantId
    };

    private static HistoryEvent HistoryEvent(Guid processId, string element, string tenantId, string data) => new()
    {
        Id = Guid.NewGuid(),
        ProcessInstanceId = processId,
        ElementId = element,
        EventType = "TASK_IO_SNAPSHOT",
        TenantId = tenantId,
        Timestamp = DateTime.UtcNow,
        Data = $"{{\"v\":\"{data}\"}}"
    };

    private static async IAsyncEnumerable<Job> Jobs(params Job[] jobs)
    {
        foreach (var job in jobs)
            yield return job;
        await Task.CompletedTask;
    }

    private static ControllerContext ContextFor(string tenantId, bool isAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test-user"),
            new("tenant_id", tenantId)
        };
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        var identity = new ClaimsIdentity(claims, "Test");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
