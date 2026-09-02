using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using VertexBPMN.Api.Migration;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "FullProductSupportAcceptance")]
public sealed class LiveProcessMigrationAcceptanceTests : IDisposable
{
    private readonly SqliteConnection _database;
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LiveProcessMigrationAcceptanceTests(CustomWebApplicationFactory _, ITestOutputHelper output)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"migration_{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30,
            Pooling = false
        }.ToString();
        _database = new SqliteConnection(connectionString);
        _database.Open();
        _factory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(connectionString)
            .WithLiveProcessMigrationEnabled();
        _client = _factory.CreateClient(output);
    }

    [Fact]
    public async Task FPS_MIGRATION_01_DryRun_Execute_Rollback_And_Target_Continuation_Are_Durable()
    {
        var sourceKey = $"migration-source-{Guid.NewGuid():N}";
        var targetKey = $"migration-target-{Guid.NewGuid():N}";
        await DeployAsync(sourceKey, "review-v1", "Review order");
        await DeployAsync(targetKey, "review-v2", "Review order");

        var first = await StartAsync(sourceKey);
        Assert.Equal("review-v1", (await TaskAsync(first.Id)).ActivityId);

        var dryRunPlan = await CreatePlanAsync(sourceKey, targetKey);
        using (var dryRunResponse = await _client.PostAsync(
                   $"/api/migration/execute/{dryRunPlan.Id}?dryRun=true", null,
                   TestContext.Current.CancellationToken))
        {
            dryRunResponse.EnsureSuccessStatusCode();
            var dryRun = await dryRunResponse.Content.ReadFromJsonAsync<MigrationExecution>(
                TestContext.Current.CancellationToken);
            Assert.NotNull(dryRun);
            Assert.Equal(MigrationStatus.Completed, dryRun.Status);
            Assert.True(dryRun.IsDryRun);
        }
        Assert.Equal("review-v1", (await TaskAsync(first.Id)).ActivityId);

        var plan = await CreatePlanAsync(sourceKey, targetKey);
        var execution = await ExecuteAsync(plan.Id);
        Assert.Equal(MigrationStatus.Completed, execution.Status);
        Assert.NotEmpty(execution.Snapshots);
        Assert.Equal("review-v2", (await TaskAsync(first.Id)).ActivityId);
        Assert.Equal(targetKey, (await InstanceAsync(first.Id)).ProcessId);

        using (var rollback = await _client.PostAsync(
                   $"/api/migration/rollback/{execution.Id}", null,
                   TestContext.Current.CancellationToken))
            rollback.EnsureSuccessStatusCode();
        Assert.Equal("review-v1", (await TaskAsync(first.Id)).ActivityId);
        Assert.Equal(sourceKey, (await InstanceAsync(first.Id)).ProcessId);
        await CompleteAsync((await TaskAsync(first.Id)).Id);
        Assert.Equal(ProcessInstanceStatus.Completed, (await InstanceAsync(first.Id)).Status);

        var second = await StartAsync(sourceKey);
        var secondPlan = await CreatePlanAsync(sourceKey, targetKey);
        var secondExecution = await ExecuteAsync(secondPlan.Id);
        Assert.Equal(MigrationStatus.Completed, secondExecution.Status);
        var migratedTask = await TaskAsync(second.Id);
        Assert.Equal("review-v2", migratedTask.ActivityId);
        await CompleteAsync(migratedTask.Id);
        var completed = await InstanceAsync(second.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Equal(targetKey, completed.ProcessId);
    }

    [Fact]
    public async Task FPS_MIGRATION_02_Event_Gateway_Tokens_Subscriptions_And_Jobs_Are_Mapped()
    {
        var sourceKey = $"migration-events-source-{Guid.NewGuid():N}";
        var targetKey = $"migration-events-target-{Guid.NewGuid():N}";
        var messageName = $"migration-message-{Guid.NewGuid():N}";
        await DeployEventGatewayAsync(sourceKey, "v1", messageName);
        await DeployEventGatewayAsync(targetKey, "v2", messageName);

        var instance = await StartAsync(sourceKey);
        var plan = await CreatePlanAsync(sourceKey, targetKey);
        Assert.Equal(MigrationStatus.Completed, (await ExecuteAsync(plan.Id)).Status);

        using var response = await _client.PostAsJsonAsync(
            "/api/vertex/message",
            new { messageName, processInstanceId = instance.Id.ToString(), variables = new { migrated = true } },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var completed = await InstanceAsync(instance.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Equal(targetKey, completed.ProcessId);
    }

    [Fact]
    public async Task FPS_MIGRATION_03_Exact_Definition_Ids_Migrate_Between_Versions_Of_The_Same_Process_Key()
    {
        var processKey = $"migration-versioned-{Guid.NewGuid():N}";
        var sourceDefinition = await DeployAsync(processKey, "review-v1", "Review order");
        var instance = await StartAsync(processKey);
        var targetDefinition = await DeployAsync(processKey, "review-v2", "Review order");

        using var planResponse = await _client.PostAsJsonAsync(
            "/api/migration/plan",
            new
            {
                fromProcessDefinitionId = sourceDefinition.Id,
                toProcessDefinitionId = targetDefinition.Id,
                options = new MigrationOptions()
            },
            TestContext.Current.CancellationToken);
        planResponse.EnsureSuccessStatusCode();
        var plan = (await planResponse.Content.ReadFromJsonAsync<MigrationPlan>(
            TestContext.Current.CancellationToken))!;

        Assert.Equal(sourceDefinition.Id, plan.FromProcessDefinitionId);
        Assert.Equal(targetDefinition.Id, plan.ToProcessDefinitionId);
        Assert.Equal(MigrationStatus.Completed, (await ExecuteAsync(plan.Id)).Status);
        Assert.Equal("review-v2", (await TaskAsync(instance.Id)).ActivityId);
        Assert.Equal(targetDefinition.Id, (await InstanceAsync(instance.Id)).ProcessDefinitionId);
    }

    [Fact]
    public async Task FPS_MIGRATION_04_Studio_Compatibility_Route_Uses_Qualified_Transactional_Migration()
    {
        var processKey = $"migration-studio-{Guid.NewGuid():N}";
        var sourceDefinition = await DeployAsync(processKey, "review-v1", "Review order");
        var instance = await StartAsync(processKey);
        var targetDefinition = await DeployAsync(processKey, "review-v2", "Review order");

        using var previewResponse = await _client.PostAsJsonAsync(
            "/api/process-migration/plan/preview",
            new
            {
                sourceProcessDefinitionId = sourceDefinition.Id.ToString(),
                targetProcessDefinitionId = targetDefinition.Id.ToString()
            },
            TestContext.Current.CancellationToken);
        previewResponse.EnsureSuccessStatusCode();
        var preview = (await previewResponse.Content.ReadFromJsonAsync<ProcessMigrationPlan>(
            TestContext.Current.CancellationToken))!;
        Assert.NotNull(preview.QualifiedPlanId);

        using var executeResponse = await _client.PostAsJsonAsync(
            "/api/process-migration/plan/execute",
            preview,
            TestContext.Current.CancellationToken);
        executeResponse.EnsureSuccessStatusCode();
        var result = (await executeResponse.Content.ReadFromJsonAsync<ProcessMigrationResult>(
            TestContext.Current.CancellationToken))!;
        Assert.True(result.Success);
        Assert.Contains(instance.Id.ToString(), result.MigratedInstanceIds!);
        Assert.Equal("review-v2", (await TaskAsync(instance.Id)).ActivityId);
        Assert.Equal(targetDefinition.Id, (await InstanceAsync(instance.Id)).ProcessDefinitionId);
    }

    [Fact]
    public async Task FPS_MIGRATION_05_Cross_Tenant_Definition_Pairs_Are_Rejected()
    {
        var source = await DeployAsync(
            $"migration-tenant-a-{Guid.NewGuid():N}", "review-a", "Review order", "tenant-a");
        var target = await DeployAsync(
            $"migration-tenant-b-{Guid.NewGuid():N}", "review-b", "Review order", "tenant-b");

        using var response = await _client.PostAsJsonAsync(
            "/api/migration/plan",
            new
            {
                fromProcessDefinitionId = source.Id,
                toProcessDefinitionId = target.Id,
                tenantId = "tenant-a",
                options = new MigrationOptions()
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<ProcessDefinition> DeployAsync(
        string processKey,
        string taskId,
        string taskName,
        string? tenantId = null)
    {
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-review" sourceRef="start" targetRef="{{taskId}}" />
                <userTask id="{{taskId}}" name="{{taskName}}" />
                <sequenceFlow id="to-end" sourceRef="{{taskId}}" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
        using var response = await _client.PostAsJsonAsync(
            "/api/repository",
            new { bpmnXml = bpmn, name = $"{processKey}.bpmn", tenantId },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProcessDefinition>(
            TestContext.Current.CancellationToken))!;
    }

    private async Task DeployEventGatewayAsync(string processKey, string suffix, string messageName)
    {
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <message id="message-{{suffix}}" name="{{messageName}}" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start-{{suffix}}" />
                <sequenceFlow id="to-wait-{{suffix}}" sourceRef="start-{{suffix}}" targetRef="wait-{{suffix}}" />
                <eventBasedGateway id="wait-{{suffix}}" name="Wait for outcome" />
                <sequenceFlow id="to-message-{{suffix}}" sourceRef="wait-{{suffix}}" targetRef="message-catch-{{suffix}}" />
                <sequenceFlow id="to-timer-{{suffix}}" sourceRef="wait-{{suffix}}" targetRef="timer-catch-{{suffix}}" />
                <intermediateCatchEvent id="message-catch-{{suffix}}" name="Wait message">
                  <messageEventDefinition messageRef="message-{{suffix}}" />
                </intermediateCatchEvent>
                <intermediateCatchEvent id="timer-catch-{{suffix}}" name="Wait timeout">
                  <timerEventDefinition><timeDuration>PT1H</timeDuration></timerEventDefinition>
                </intermediateCatchEvent>
                <sequenceFlow id="message-end-{{suffix}}" sourceRef="message-catch-{{suffix}}" targetRef="end-{{suffix}}" />
                <sequenceFlow id="timer-end-{{suffix}}" sourceRef="timer-catch-{{suffix}}" targetRef="end-{{suffix}}" />
                <endEvent id="end-{{suffix}}" />
              </process>
            </definitions>
            """;
        using var response = await _client.PostAsJsonAsync(
            "/api/repository",
            new { bpmnXml = bpmn, name = $"{processKey}.bpmn", tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<RuntimeInstance> StartAsync(string processKey)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/runtime/start",
            new { processDefinitionKey = processKey, variables = new { orderId = "42" }, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RuntimeInstance>(TestContext.Current.CancellationToken))!;
    }

    private async Task<MigrationPlan> CreatePlanAsync(string sourceKey, string targetKey)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/migration/plan",
            new { fromProcessKey = sourceKey, toProcessKey = targetKey, options = new MigrationOptions() },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationPlan>(TestContext.Current.CancellationToken))!;
    }

    private async Task<MigrationExecution> ExecuteAsync(Guid planId)
    {
        using var response = await _client.PostAsync(
            $"/api/migration/execute/{planId}", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MigrationExecution>(TestContext.Current.CancellationToken))!;
    }

    private async Task<PersistedTask> TaskAsync(Guid instanceId) =>
        Assert.Single(await _client.GetFromJsonAsync<List<PersistedTask>>(
            $"/api/task?processInstanceId={instanceId}", TestContext.Current.CancellationToken) ?? []);

    private async Task<RuntimeInstance> InstanceAsync(Guid instanceId) =>
        (await _client.GetFromJsonAsync<RuntimeInstance>(
            $"/api/runtime/{instanceId}", TestContext.Current.CancellationToken))!;

    private async Task CompleteAsync(Guid taskId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/task/{taskId}/complete",
            new { variables = new { approved = true }, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _database.Dispose();
    }

    private sealed record RuntimeInstance(
        Guid Id,
        Guid ProcessDefinitionId,
        string ProcessId,
        ProcessInstanceStatus Status);
    private sealed record PersistedTask(Guid Id, Guid ProcessInstanceId, string ActivityId, string Name, UserTaskStatus Status);
}
