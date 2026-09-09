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

    [Fact]
    public async Task ForeignDebugSession_CannotBeReadOrMutated()
    {
        var instance = new ProcessInstance { Id = Guid.NewGuid(), TenantId = TenantB };
        var session = new VertexBPMN.Domain.Entities.Debugging.DebugSession
        { Id = Guid.NewGuid(), ProcessInstanceId = instance.Id };
        var runtime = new Mock<IRuntimeService>(MockBehavior.Strict);
        runtime.Setup(x => x.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ProcessInstance?>(instance));
        var debug = new Mock<IVisualDebuggingService>(MockBehavior.Strict);
        debug.Setup(x => x.GetDebugSessionAsync(session.Id)).ReturnsAsync(session);
        var controller = new VisualDebugController(debug.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VisualDebugController>.Instance, runtime.Object)
        { ControllerContext = ContextFor(TenantA) };
        Assert.IsType<NotFoundResult>((await controller.GetDebugSession(session.Id)).Result);
        Assert.IsType<NotFoundResult>(await controller.StopDebuggingSession(session.Id));
        Assert.IsType<NotFoundResult>(await controller.SetBreakpoint(session.Id, "task"));
        Assert.IsType<NotFoundResult>(await controller.RemoveBreakpoint(session.Id, "task"));
        Assert.IsType<NotFoundResult>((await controller.StepOver(session.Id)).Result);
        Assert.IsType<NotFoundResult>((await controller.StepInto(session.Id)).Result);
        Assert.IsType<NotFoundResult>((await controller.StepOut(session.Id)).Result);
        Assert.IsType<NotFoundResult>((await controller.ContinueExecution(session.Id)).Result);
        Assert.IsType<NotFoundResult>((await controller.InspectVariables(session.Id)).Result);
        Assert.IsType<NotFoundResult>((await controller.StartDebuggingSession(instance.Id)).Result);
        Assert.IsType<NotFoundResult>((await controller.GetExecutionTrace(instance.Id)).Result);
        debug.Verify(x => x.GetDebugSessionAsync(session.Id), Times.Exactly(9));
        debug.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("tenant-b")]
    [InlineData(null)]
    public async Task DebuggerState_ForeignOrUnscopedInstance_IsNotExposed(string? instanceTenant)
    {
        var instance = new ProcessInstance { Id = Guid.NewGuid(), TenantId = instanceTenant };
        var runtime = new Mock<IRuntimeService>(MockBehavior.Strict);
        runtime.Setup(x => x.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ProcessInstance?>(instance));
        var controller = new VisualDebuggerController(runtime.Object, Mock.Of<IVisualDebugStepService>())
        { ControllerContext = ContextFor(TenantA) };
        Assert.IsType<NotFoundResult>((await controller.GetInstanceState(instance.Id, default)).Result);
    }

    [Fact]
    public async Task AnalyticsExport_PinsTenantToClaim()
    {
        var service = new Mock<IPredictiveAnalyticsService>(MockBehavior.Strict);
        service.Setup(x => x.ExportTrainingDataAsync(null, TenantA)).ReturnsAsync("safe-data");
        var controller = new MLAnalyticsController(service.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MLAnalyticsController>.Instance)
        { ControllerContext = ContextFor(TenantA) };
        Assert.IsType<FileContentResult>(await controller.ExportTrainingData(null, TenantB));
        service.Verify(x => x.ExportTrainingDataAsync(null, TenantA), Times.Once);
    }

    [Fact]
    public async Task AnalyticsExport_MissingTenant_DoesNotQueryGlobalData()
    {
        var service = new Mock<IPredictiveAnalyticsService>(MockBehavior.Strict);
        var controller = new MLAnalyticsController(service.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MLAnalyticsController>.Instance)
        { ControllerContext = ContextFor("") };
        Assert.IsType<ForbidResult>(await controller.ExportTrainingData());
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Jobs_WithoutValidTenant_FailClosed(string? tenant)
    {
        var context = ContextFor(tenant ?? "");
        if (tenant is null)
        {
            var identity = (ClaimsIdentity)context.HttpContext.User.Identity!;
            identity.RemoveClaim(identity.FindFirst("tenant_id")!);
        }
        var repo = new Mock<IJobRepository>(MockBehavior.Strict);
        var controller = new VertexJobController(repo.Object) { ControllerContext = context };
        var rows = new List<JobDto>();
        await foreach (var row in controller.GetAll()) rows.Add(row);
        Assert.Empty(rows);
        Assert.IsType<ForbidResult>((await controller.GetById(Guid.NewGuid())).Result);
        repo.VerifyNoOtherCalls();
    }

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
