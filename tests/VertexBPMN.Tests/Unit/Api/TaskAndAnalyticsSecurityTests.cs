using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Dto;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class TaskAndAnalyticsSecurityTests
{
    [Fact]
    public async Task TaskGetById_ForDifferentTenant_ReturnsForbid()
    {
        var task = new UserTask { Id = Guid.NewGuid(), TenantId = "tenant-b" };
        var service = new Mock<ITaskService>();
        service.Setup(x => x.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<UserTask?>(task));
        var controller = new TaskController(service.Object)
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = await controller.GetById(task.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task TaskClaim_ForDifferentTenant_ReturnsForbidWithoutMutation()
    {
        var taskId = Guid.NewGuid();
        var service = new Mock<ITaskService>();
        service.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<UserTask?>(new UserTask { Id = taskId, TenantId = "tenant-b" }));
        var controller = new TaskController(service.Object)
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = await controller.Claim(taskId, new TaskController.ClaimRequest("user-b"));

        Assert.IsType<ForbidResult>(result);
        service.Verify(x => x.ClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FormSchemaUpdate_ReturnsNotImplementedInsteadOfInMemorySuccess()
    {
        var taskId = Guid.NewGuid();
        var service = new Mock<ITaskService>();
        service.Setup(x => x.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<UserTask?>(new UserTask { Id = taskId, TenantId = "tenant-a" }));
        var controller = new VertexTaskController(service.Object)
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = await controller.UpdateFormSchema(
            taskId,
            new VertexTaskController.UpdateFormSchemaRequest("form", "{}"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, problem.StatusCode);
    }

    [Fact]
    public void PredictiveAnalytics_ReturnsNotImplementedInsteadOfSyntheticPrediction()
    {
        var controller = new AnalyticsController(Mock.Of<IProcessMiningEventSink>(), null!)
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = controller.PredictDuration(new PredictDurationRequest([1, 2, 3]));

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status501NotImplemented, problem.StatusCode);
    }

    [Fact]
    public async Task HistoryList_ForNonAdmin_UsesClaimTenantInsteadOfRequestedTenant()
    {
        var historyEvent = new HistoryEvent { Id = Guid.NewGuid(), TenantId = "tenant-a" };
        var service = new Mock<IHistoryService>();
        service.Setup(x => x.ListAsync("tenant-a", It.IsAny<CancellationToken>()))
            .Returns(Events(historyEvent));
        var controller = new HistoryController(service.Object)
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = await controller.List("tenant-b");

        var events = Assert.IsType<List<HistoryEvent>>(result.Value);
        Assert.Single(events);
        Assert.Equal(historyEvent.Id, events[0].Id);
        service.Verify(x => x.ListAsync("tenant-a", It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.ListAsync("tenant-b", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepositoryDelete_ForDifferentTenant_ReturnsForbidWithoutMutation()
    {
        var definitionId = Guid.NewGuid();
        var service = new Mock<IRepositoryService>();
        service.Setup(x => x.GetByIdAsync(definitionId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ProcessDefinition?>(new ProcessDefinition
            {
                Id = definitionId,
                TenantId = "tenant-b"
            }));
        var controller = new RepositoryController(service.Object, Mock.Of<IWorkflowTriggerService>())
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = await controller.Delete(definitionId);

        Assert.IsType<ForbidResult>(result);
        service.Verify(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VertexTaskList_ForNonAdmin_UsesClaimTenantInsteadOfRequestedTenant()
    {
        var task = new UserTask { Id = Guid.NewGuid(), TenantId = "tenant-a" };
        var service = new Mock<ITaskService>();
        service.Setup(x => x.ListAsync(null, null, "tenant-a", It.IsAny<CancellationToken>()))
            .Returns(Tasks(task));
        var controller = new VertexTaskController(service.Object)
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = await controller.GetAll(null, null, "tenant-b");

        var tasks = Assert.IsType<List<TaskDto>>(result.Value);
        Assert.Single(tasks);
        service.Verify(x => x.ListAsync(null, null, "tenant-a", It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.ListAsync(null, null, "tenant-b", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDefinitionXmlUpdate_ReturnsNotImplementedWithoutMutation()
    {
        var service = new Mock<IRepositoryService>();
        var controller = new VertexProcessDefinitionController(service.Object)
        {
            ControllerContext = ContextFor("tenant-a")
        };

        var result = await controller.UpdateXml(
            Guid.NewGuid(),
            new VertexProcessDefinitionController.UpdateXmlRequest("<definitions />"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, problem.StatusCode);
        service.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async IAsyncEnumerable<HistoryEvent> Events(params HistoryEvent[] events)
    {
        foreach (var historyEvent in events)
            yield return historyEvent;

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<UserTask> Tasks(params UserTask[] tasks)
    {
        foreach (var task in tasks)
            yield return task;

        await Task.CompletedTask;
    }

    private static ControllerContext ContextFor(string tenantId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "test-user"), new Claim("tenant_id", tenantId)],
            "Test");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
