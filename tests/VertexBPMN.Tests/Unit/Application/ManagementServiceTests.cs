using Microsoft.Extensions.DependencyInjection;
using Moq;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class ManagementServiceTests
{
    [Fact]
    public async Task SuspendProcessInstanceAsync_DelegatesToPersistentRuntimeForTenantInstance()
    {
        var instance = CreateInstance(ProcessInstanceStatus.Running);
        var runtime = new Mock<IRuntimeService>();
        runtime.Setup(service => service.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var service = CreateService(runtime: runtime);

        await service.SuspendProcessInstanceAsync(instance.Id, instance.TenantId, TestContext.Current.CancellationToken);

        runtime.Verify(
            candidate => candidate.SuspendAsync(instance.Id, TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ResumeProcessInstanceAsync_DelegatesToPersistentRuntimeForTenantInstance()
    {
        var instance = CreateInstance(ProcessInstanceStatus.Suspended);
        var runtime = new Mock<IRuntimeService>();
        runtime.Setup(service => service.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var service = CreateService(runtime: runtime);

        await service.ResumeProcessInstanceAsync(instance.Id, instance.TenantId, TestContext.Current.CancellationToken);

        runtime.Verify(
            candidate => candidate.ResumeAsync(instance.Id, TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DeleteProcessInstanceAsync_DeletesTenantInstance()
    {
        var instance = CreateInstance(ProcessInstanceStatus.Running);
        var runtime = new Mock<IRuntimeService>();
        runtime.Setup(service => service.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var service = CreateService(runtime: runtime);

        await service.DeleteProcessInstanceAsync(instance.Id, instance.TenantId, TestContext.Current.CancellationToken);

        runtime.Verify(
            candidate => candidate.DeleteAsync(instance.Id, TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SuspendProcessInstanceAsync_RejectsCrossTenantAccess()
    {
        var instance = CreateInstance(ProcessInstanceStatus.Running);
        var runtime = new Mock<IRuntimeService>();
        runtime.Setup(service => service.GetByIdAsync(instance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(instance);
        var service = CreateService(runtime: runtime);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.SuspendProcessInstanceAsync(instance.Id, "other-tenant", TestContext.Current.CancellationToken));

        runtime.Verify(
            candidate => candidate.SuspendAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ManagementService CreateService(
        Mock<IRuntimeService>? runtime = null,
        Mock<IProcessInstanceRepository>? repository = null,
        Mock<IProcessMiningEventSink>? eventSink = null)
    {
        var services = new ServiceCollection()
            .AddSingleton((runtime ?? new Mock<IRuntimeService>()).Object)
            .AddSingleton((repository ?? new Mock<IProcessInstanceRepository>()).Object)
            .AddSingleton((eventSink ?? new Mock<IProcessMiningEventSink>()).Object)
            .BuildServiceProvider();
        return new ManagementService(services, Mock.Of<IRuntimeMetricsReader>());
    }

    private static ProcessInstance CreateInstance(ProcessInstanceStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = "tenant-a",
        ProcessId = "management-process",
        Status = status,
        State = status.ToString()
    };
}
