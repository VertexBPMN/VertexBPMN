using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Application;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class PollingSchedulerServiceTests
{
    private static Mock<IRuntimeService> BuildRuntimeService(string key)
    {
        var runtimeService = new Mock<IRuntimeService>();
        runtimeService.Setup(x => x.StartProcessByKeyAsync(
                It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string>()))
            .ReturnsAsync(new ProcessInstance { ProcessId = key, Id = Guid.NewGuid() });
        return runtimeService;
    }

    private static (PollingSchedulerService scheduler, Mock<IRuntimeService> runtimeService) Build(
        Mock<IPollingTriggerRepository> repository, Mock<IConnectorRuntime> connectorRuntime, Mock<IRuntimeService> runtimeService)
    {
        var poller = new PollingTriggerPoller(connectorRuntime.Object, runtimeService.Object, NullLogger<PollingTriggerPoller>.Instance);
        var services = new ServiceCollection()
            .AddSingleton(repository.Object)
            .AddSingleton(poller)
            .BuildServiceProvider();
        var scheduler = new PollingSchedulerService(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<PollingSchedulerService>.Instance);
        return (scheduler, runtimeService);
    }

    private static PollingTriggerRecord DueTrigger(string key) => new()
    {
        TenantId = "tenant-a",
        Name = "poll",
        ProcessDefinitionKey = key,
        ConnectorType = "http",
        ConnectorAttributesJson = "{}",
        NextDueAt = DateTime.UtcNow.AddSeconds(-1),
        CursorStateJson = "{}"
    };

    [Fact]
    public async Task RunIteration_LeasesDueTrigger_AndStartsInstanceWhenNewData()
    {
        var key = $"poll-{Guid.NewGuid():N}";
        var repository = new Mock<IPollingTriggerRepository>();
        repository.Setup(x => x.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { DueTrigger(key) });
        repository.Setup(x => x.TryLeaseAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.UpdateAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var connectorRuntime = new Mock<IConnectorRuntime>();
        connectorRuntime.Setup(x => x.ExecuteAsync(It.IsAny<ConnectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectorExecutionResult(true, 200, new Dictionary<string, object> { ["lastSeenId"] = 7 }));

        var runtimeService = BuildRuntimeService(key);
        var (scheduler, _) = Build(repository, connectorRuntime, runtimeService);

        await scheduler.RunIterationAsync(TestContext.Current.CancellationToken);

        runtimeService.Verify(x => x.StartProcessByKeyAsync(
            key, It.IsAny<IDictionary<string, object>>(), null, "tenant-a", It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
        repository.Verify(x => x.UpdateAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunIteration_DoesNotStartInstanceWhenUnchanged()
    {
        var key = $"poll-{Guid.NewGuid():N}";
        var trigger = DueTrigger(key);
        trigger.CursorStateJson = "{\"lastSeenId\":7}";
        var repository = new Mock<IPollingTriggerRepository>();
        repository.Setup(x => x.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { trigger });
        repository.Setup(x => x.TryLeaseAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.UpdateAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var connectorRuntime = new Mock<IConnectorRuntime>();
        connectorRuntime.Setup(x => x.ExecuteAsync(It.IsAny<ConnectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectorExecutionResult(true, 200, new Dictionary<string, object> { ["lastSeenId"] = 7 }));

        var runtimeService = BuildRuntimeService(key);
        var (scheduler, _) = Build(repository, connectorRuntime, runtimeService);

        await scheduler.RunIterationAsync(TestContext.Current.CancellationToken);

        runtimeService.Verify(x => x.StartProcessByKeyAsync(
            It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Never);
        repository.Verify(x => x.UpdateAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunIteration_OnFailure_IncrementsFailuresAndAppliesBackoff()
    {
        var key = $"poll-{Guid.NewGuid():N}";
        var repository = new Mock<IPollingTriggerRepository>();
        repository.Setup(x => x.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { DueTrigger(key) });
        repository.Setup(x => x.TryLeaseAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        PollingTriggerRecord? captured = null;
        repository.Setup(x => x.UpdateAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<CancellationToken>()))
            .Callback<PollingTriggerRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        var connectorRuntime = new Mock<IConnectorRuntime>();
        connectorRuntime.Setup(x => x.ExecuteAsync(It.IsAny<ConnectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectorExecutionResult(false, 503, new Dictionary<string, object>(), "remote_server_error"));

        var runtimeService = BuildRuntimeService(key);
        var (scheduler, _) = Build(repository, connectorRuntime, runtimeService);

        await scheduler.RunIterationAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(1, captured!.ConsecutiveFailures);
        Assert.True(captured.NextDueAt > DateTime.UtcNow.AddSeconds(50)); // backoff >= interval (60s)
        runtimeService.Verify(x => x.StartProcessByKeyAsync(
            It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunIteration_SkipsTriggerAlreadyLeasedByAnotherWorker()
    {
        var key = $"poll-{Guid.NewGuid():N}";
        var repository = new Mock<IPollingTriggerRepository>();
        repository.Setup(x => x.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { DueTrigger(key) });
        // lease is held by another worker -> this instance must not run it
        repository.Setup(x => x.TryLeaseAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var connectorRuntime = new Mock<IConnectorRuntime>();
        var runtimeService = BuildRuntimeService(key);
        var (scheduler, _) = Build(repository, connectorRuntime, runtimeService);

        await scheduler.RunIterationAsync(TestContext.Current.CancellationToken);

        runtimeService.Verify(x => x.StartProcessByKeyAsync(
            It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Never);
        repository.Verify(x => x.UpdateAsync(It.IsAny<PollingTriggerRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
