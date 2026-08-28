using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "FullProductSupportAcceptance")]
public sealed class CompensationSemanticsAcceptanceTests : IDisposable
{
    private readonly SqliteConnection _database;
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CompensationSemanticsAcceptanceTests(CustomWebApplicationFactory _, ITestOutputHelper output)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"compensation_{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30,
            Pooling = false
        }.ToString();
        _database = new SqliteConnection(connectionString);
        _database.Open();
        _factory = new CustomWebApplicationFactory().WithPersistentBpmnDatabase(connectionString);
        _client = _factory.CreateClient(output);
    }

    [Fact]
    public async Task FPS_COMPENSATION_01_Handlers_Run_Sequentially_In_Reverse_Completion_Order()
    {
        var key = $"compensation-order-{Guid.NewGuid():N}";
        var instance = await DeployAndStartAsync(key, $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{key}}" isExecutable="true">
                <startEvent id="start"/><sequenceFlow id="to-flight" sourceRef="start" targetRef="flight"/>
                <task id="flight"/><boundaryEvent id="comp-flight" attachedToRef="flight" cancelActivity="false"><compensateEventDefinition/></boundaryEvent>
                <sequenceFlow id="to-hotel" sourceRef="flight" targetRef="hotel"/>
                <task id="hotel"/><boundaryEvent id="comp-hotel" attachedToRef="hotel" cancelActivity="false"><compensateEventDefinition/></boundaryEvent>
                <sequenceFlow id="to-throw" sourceRef="hotel" targetRef="throw"/>
                <intermediateThrowEvent id="throw"><compensateEventDefinition/></intermediateThrowEvent>
                <sequenceFlow id="to-end" sourceRef="throw" targetRef="end"/><endEvent id="end"/>
                <userTask id="undo-flight" name="Undo flight" isForCompensation="true"/>
                <userTask id="undo-hotel" name="Undo hotel" isForCompensation="true"/>
                <association id="handle-flight" sourceRef="comp-flight" targetRef="undo-flight" associationDirection="One"/>
                <association id="handle-hotel" sourceRef="comp-hotel" targetRef="undo-hotel" associationDirection="One"/>
              </process>
            </definitions>
            """);

        Assert.Equal(ProcessInstanceStatus.Running, instance.Status);
        var activeTasks = await TasksAsync(instance.Id);
        Assert.True(activeTasks.Count > 0, await CompensationStateAsync(instance.Id));
        var first = Assert.Single(activeTasks);
        Assert.Equal("Undo hotel", first.Name);
        await CompleteAsync(first.Id);
        var second = Assert.Single(await TasksAsync(instance.Id));
        Assert.Equal("Undo flight", second.Name);
        await CompleteAsync(second.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, (await InstanceAsync(instance.Id)).Status);
    }

    [Fact]
    public async Task FPS_COMPENSATION_02_Implicit_Throw_Is_Isolated_To_Its_Current_Scope()
    {
        var key = $"compensation-scope-{Guid.NewGuid():N}";
        var instance = await DeployAndStartAsync(key, $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{key}}" isExecutable="true">
                <startEvent id="start"/><sequenceFlow id="to-booking" sourceRef="start" targetRef="booking"/>
                <subProcess id="booking">
                  <startEvent id="booking-start"/><sequenceFlow id="to-inner" sourceRef="booking-start" targetRef="inner"/>
                  <task id="inner"/><boundaryEvent id="comp-inner" attachedToRef="inner" cancelActivity="false"><compensateEventDefinition/></boundaryEvent>
                  <sequenceFlow id="inner-end" sourceRef="inner" targetRef="booking-end"/><endEvent id="booking-end"/>
                  <userTask id="undo-inner" name="Undo inner" isForCompensation="true"/>
                  <association id="handle-inner" sourceRef="comp-inner" targetRef="undo-inner" associationDirection="One"/>
                </subProcess>
                <sequenceFlow id="to-outer" sourceRef="booking" targetRef="outer"/>
                <task id="outer"/><boundaryEvent id="comp-outer" attachedToRef="outer" cancelActivity="false"><compensateEventDefinition/></boundaryEvent>
                <sequenceFlow id="to-throw" sourceRef="outer" targetRef="throw"/>
                <intermediateThrowEvent id="throw"><compensateEventDefinition/></intermediateThrowEvent>
                <sequenceFlow id="to-end" sourceRef="throw" targetRef="end"/><endEvent id="end"/>
                <userTask id="undo-outer" name="Undo outer" isForCompensation="true"/>
                <association id="handle-outer" sourceRef="comp-outer" targetRef="undo-outer" associationDirection="One"/>
              </process>
            </definitions>
            """);

        var task = Assert.Single(await TasksAsync(instance.Id));
        Assert.Equal("Undo outer", task.Name);
        await CompleteAsync(task.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, (await InstanceAsync(instance.Id)).Status);
    }

    [Fact]
    public async Task FPS_COMPENSATION_03_Completed_Subprocess_Uses_Compensation_Event_Subprocess_Handler()
    {
        var key = $"compensation-event-subprocess-{Guid.NewGuid():N}";
        var instance = await DeployAndStartAsync(key, $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{key}}" isExecutable="true">
                <startEvent id="start"/><sequenceFlow id="to-booking" sourceRef="start" targetRef="booking"/>
                <subProcess id="booking">
                  <startEvent id="booking-start"/><sequenceFlow id="to-work" sourceRef="booking-start" targetRef="work"/>
                  <task id="work"/><sequenceFlow id="work-end" sourceRef="work" targetRef="booking-end"/><endEvent id="booking-end"/>
                  <subProcess id="booking-compensation" triggeredByEvent="true">
                    <startEvent id="compensation-start" isInterrupting="false"><compensateEventDefinition/></startEvent>
                    <sequenceFlow id="to-undo" sourceRef="compensation-start" targetRef="undo-booking"/>
                    <userTask id="undo-booking" name="Undo booking" isForCompensation="true"/>
                    <sequenceFlow id="undo-end" sourceRef="undo-booking" targetRef="compensation-end"/><endEvent id="compensation-end"/>
                  </subProcess>
                </subProcess>
                <sequenceFlow id="to-throw" sourceRef="booking" targetRef="throw"/>
                <intermediateThrowEvent id="throw"><compensateEventDefinition/></intermediateThrowEvent>
                <sequenceFlow id="to-end" sourceRef="throw" targetRef="end"/><endEvent id="end"/>
              </process>
            </definitions>
            """);

        var task = Assert.Single(await TasksAsync(instance.Id));
        Assert.Equal("Undo booking", task.Name);
        await CompleteAsync(task.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, (await InstanceAsync(instance.Id)).Status);
    }

    [Fact]
    public async Task FPS_COMPENSATION_04_Transaction_Cancel_Completes_Compensation_Before_Cancel_Boundary()
    {
        var key = $"compensation-cancel-{Guid.NewGuid():N}";
        var instance = await DeployAndStartAsync(key, $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{key}}" isExecutable="true">
                <startEvent id="start"/><sequenceFlow id="to-payment" sourceRef="start" targetRef="payment"/>
                <transaction id="payment">
                  <startEvent id="payment-start"/><sequenceFlow id="to-charge" sourceRef="payment-start" targetRef="charge"/>
                  <task id="charge"/><boundaryEvent id="comp-charge" attachedToRef="charge" cancelActivity="false"><compensateEventDefinition/></boundaryEvent>
                  <sequenceFlow id="to-cancel" sourceRef="charge" targetRef="cancel"/><endEvent id="cancel"><cancelEventDefinition/></endEvent>
                  <userTask id="undo-charge" name="Undo charge" isForCompensation="true"/>
                  <association id="handle-charge" sourceRef="comp-charge" targetRef="undo-charge" associationDirection="One"/>
                </transaction>
                <boundaryEvent id="cancel-boundary" attachedToRef="payment"><cancelEventDefinition/></boundaryEvent>
                <sequenceFlow id="to-resolution" sourceRef="cancel-boundary" targetRef="resolution"/>
                <userTask id="resolution" name="Resolve cancellation"/>
                <sequenceFlow id="resolution-end" sourceRef="resolution" targetRef="end"/><endEvent id="end"/>
              </process>
            </definitions>
            """);

        var compensation = Assert.Single(await TasksAsync(instance.Id));
        Assert.Equal("Undo charge", compensation.Name);
        await CompleteAsync(compensation.Id);
        var resolution = Assert.Single(await TasksAsync(instance.Id));
        Assert.Equal("Resolve cancellation", resolution.Name);
        await CompleteAsync(resolution.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, (await InstanceAsync(instance.Id)).Status);
    }

    private async Task<RuntimeInstance> DeployAndStartAsync(string key, string bpmn)
    {
        using (var deployment = await _client.PostAsJsonAsync(
                   "/api/repository",
                   new { bpmnXml = bpmn, name = $"{key}.bpmn", tenantId = (string?)null },
                   TestContext.Current.CancellationToken))
            deployment.EnsureSuccessStatusCode();
        using var start = await _client.PostAsJsonAsync(
            "/api/runtime/start",
            new { processDefinitionKey = key, variables = new { }, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        return (await start.Content.ReadFromJsonAsync<RuntimeInstance>(
            TestContext.Current.CancellationToken))!;
    }

    private async Task<List<PersistedTask>> TasksAsync(Guid instanceId) =>
        await _client.GetFromJsonAsync<List<PersistedTask>>(
            $"/api/task?processInstanceId={instanceId}", TestContext.Current.CancellationToken) ?? [];

    private async Task CompleteAsync(Guid taskId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/task/{taskId}/complete",
            new { variables = new { }, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<RuntimeInstance> InstanceAsync(Guid instanceId) =>
        (await _client.GetFromJsonAsync<RuntimeInstance>(
            $"/api/runtime/{instanceId}", TestContext.Current.CancellationToken))!;

    private async Task<string> CompensationStateAsync(Guid instanceId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var subscriptions = await db.EventSubscriptions.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instanceId)
            .Select(item => new { item.ActivityId, item.EventName, item.State, item.ExecutionTokenId })
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var tokens = await db.ExecutionTokens.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instanceId)
            .Select(item => new { item.CurrentNodeId, item.NodeType, item.State })
            .ToArrayAsync(TestContext.Current.CancellationToken);
        return System.Text.Json.JsonSerializer.Serialize(new { subscriptions, tokens });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _database.Dispose();
    }

    private sealed record RuntimeInstance(Guid Id, ProcessInstanceStatus Status);
    private sealed record PersistedTask(Guid Id, string ActivityId, string Name, UserTaskStatus Status);
}
