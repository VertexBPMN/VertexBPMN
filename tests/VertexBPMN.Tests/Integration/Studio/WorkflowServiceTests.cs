using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class WorkflowServiceTests
{
    [Fact]
    public async Task StartProcess_ForwardsTenantAndArgumentsToEngineService()
    {
        var engine = new Mock<IBpmnEngineService>(MockBehavior.Strict);
        var variables = new Dictionary<string, object?> { ["amount"] = 42 };
        engine
            .Setup(service => service.StartProcessAsync("invoice", variables, "order-42", "tenant-a"))
            .ReturnsAsync((ProcessInstance)null!);

        var workflow = new WorkflowService(engine.Object);

        await workflow.StartProcessAsync("invoice", variables, "order-42", "tenant-a");

        engine.Verify(service => service.StartProcessAsync("invoice", variables, "order-42", "tenant-a"), Times.Once);
    }
}
