using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "Phase1Acceptance")]
public sealed class BpmnCoreLifecycleContractTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly SqliteConnection _bpmnDatabase;
    private readonly HttpClient _client;

    public BpmnCoreLifecycleContractTests(
        CustomWebApplicationFactory _,
        ITestOutputHelper output)
    {
        _bpmnDatabase = new SqliteConnection($"Data Source=file:phase1_{Guid.NewGuid():N}?mode=memory&cache=shared");
        _bpmnDatabase.Open();
        _factory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(_bpmnDatabase)
            .WithBackgroundJobsEnabled();
        _output = output;
        _client = _factory.CreateClient(output);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _bpmnDatabase.Dispose();
    }

    [Fact]
    public async Task P1_AC_01_Deploy_Start_Service_UserTask_Complete_End()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processKey = $"phase1-core-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-score" sourceRef="start" targetRef="score" />
                <serviceTask id="score" name="Calculate score" implementation="calculateScore" />
                <sequenceFlow id="to-review" sourceRef="score" targetRef="review" />
                <userTask id="review" name="Review application" />
                <sequenceFlow id="to-end" sourceRef="review" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        using var deployResponse = await _client.PostAsJsonAsync(
            "/api/repository",
            new { bpmnXml = bpmn, name = $"{processKey}.bpmn", tenantId = (string?)null },
            cancellationToken);
        deployResponse.EnsureSuccessStatusCode();
        var definition = await deployResponse.Content.ReadFromJsonAsync<DeployedProcess>(cancellationToken);
        Assert.NotNull(definition);
        Assert.Equal(processKey, definition.Key);

        using var startResponse = await _client.PostAsJsonAsync(
            "/api/runtime/start",
            new
            {
                processDefinitionKey = processKey,
                variables = new Dictionary<string, object>
                {
                    ["applicantName"] = "Ada Lovelace",
                    ["age"] = 36
                },
                businessKey = $"acceptance-{Guid.NewGuid():N}",
                tenantId = (string?)null
            },
            cancellationToken);
        startResponse.EnsureSuccessStatusCode();
        var started = await startResponse.Content.ReadFromJsonAsync<RuntimeInstance>(cancellationToken);
        Assert.NotNull(started);
        Assert.Equal(definition.Id, started.ProcessDefinitionId);

        Assert.True(
            started.Variables.ContainsKey("creditScore"),
            "The calculateScore service-task handler was not executed by the public runtime path.");

        var tasks = await _client.GetFromJsonAsync<List<PersistedUserTask>>(
            $"/api/task?processInstanceId={started.Id}",
            cancellationToken);
        var reviewTask = Assert.Single(tasks ?? []);
        Assert.Equal("Review application", reviewTask.Name);
        Assert.Equal(UserTaskStatus.Pending, reviewTask.Status);

        using var completeResponse = await _client.PostAsJsonAsync(
            $"/api/task/{reviewTask.Id}/complete",
            new
            {
                variables = new Dictionary<string, object> { ["approved"] = true },
                tenantId = (string?)null
            },
            cancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, completeResponse.StatusCode);

        var completed = await _client.GetFromJsonAsync<RuntimeInstance>(
            $"/api/runtime/{started.Id}",
            cancellationToken);
        Assert.NotNull(completed);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.NotNull(completed.EndedAt);
        Assert.Empty(completed.ActiveTasks);
        Assert.Empty(completed.ActiveTokens);
    }

    [Fact]
    public async Task P1_AC_02A_TimerCatch_Waits_Until_Due_Then_Resumes_Exactly_Once()
    {
        var processKey = $"phase1-timer-catch-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-wait" sourceRef="start" targetRef="wait" />
                <intermediateCatchEvent id="wait" name="Wait for due date">
                  <timerEventDefinition><timeDuration>PT0.25S</timeDuration></timerEventDefinition>
                </intermediateCatchEvent>
                <sequenceFlow id="to-end" sourceRef="wait" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);

        Assert.Equal(ProcessInstanceStatus.Running, started.Status);
        Assert.True(
            started.ActiveTokens.Contains("wait"),
            "Timer catch wait token was not persisted by the public runtime path.");

        var jobs = await _client.GetFromJsonAsync<List<PersistedJob>>(
            "/api/vertex/job",
            TestContext.Current.CancellationToken);
        var timer = Assert.Single((jobs ?? []).Where(job => job.ProcessInstanceId == started.Id.ToString()));
        Assert.Equal("timer", timer.JobType, ignoreCase: true);
        Assert.True(timer.DueDate > DateTime.UtcNow.AddSeconds(-1));

        var beforeDue = await GetInstanceAsync(started.Id);
        Assert.Equal(ProcessInstanceStatus.Running, beforeDue.Status);
        Assert.Contains("wait", beforeDue.ActiveTokens);

        var completed = await WaitForStatusAsync(started.Id, ProcessInstanceStatus.Completed, TimeSpan.FromSeconds(8));
        Assert.NotNull(completed.EndedAt);
        Assert.Empty(completed.ActiveTokens);

        var remainingJobs = await _client.GetFromJsonAsync<List<PersistedJob>>(
            "/api/vertex/job",
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(remainingJobs ?? [], job => job.ProcessInstanceId == started.Id.ToString());
    }

    [Fact]
    public async Task P1_AC_02B_BoundaryTimer_Interrupts_Waiting_Task_And_Resumes_Exactly_Once()
    {
        var processKey = $"phase1-timer-boundary-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-review" sourceRef="start" targetRef="review" />
                <userTask id="review" name="Review before timeout" />
                <boundaryEvent id="timeout" attachedToRef="review" cancelActivity="true">
                  <timerEventDefinition><timeDuration>PT0.25S</timeDuration></timerEventDefinition>
                </boundaryEvent>
                <sequenceFlow id="to-timeout-end" sourceRef="timeout" targetRef="timeout-end" />
                <endEvent id="timeout-end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        var tasks = await GetTasksAsync(started.Id);
        Assert.True(
            tasks.Count == 1,
            "Boundary timer precondition failed: the attached user task was not persisted.");
        Assert.Equal("Review before timeout", tasks[0].Name);

        var jobs = await _client.GetFromJsonAsync<List<PersistedJob>>(
            "/api/vertex/job",
            TestContext.Current.CancellationToken);
        Assert.Single((jobs ?? []).Where(job => job.ProcessInstanceId == started.Id.ToString()));

        var completed = await WaitForStatusAsync(started.Id, ProcessInstanceStatus.Completed, TimeSpan.FromSeconds(8));
        Assert.Empty(completed.ActiveTasks);
        Assert.Empty(completed.ActiveTokens);
        Assert.Empty(await GetTasksAsync(started.Id));
    }

    [Fact]
    public async Task P1_AC_03A_Message_Waits_Correlates_Only_Matching_Subscription_And_Resumes_Once()
    {
        var processKey = $"phase1-message-{Guid.NewGuid():N}";
        var messageName = $"payment-received-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <message id="payment-message" name="{{messageName}}" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-message" sourceRef="start" targetRef="await-message" />
                <intermediateCatchEvent id="await-message" name="Await payment">
                  <messageEventDefinition messageRef="payment-message" />
                </intermediateCatchEvent>
                <sequenceFlow id="to-end" sourceRef="await-message" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        Assert.True(
            started.ActiveTokens.Contains("await-message"),
            "Message subscription wait token was not persisted by the public runtime path.");

        using var wrongCorrelation = await _client.PostAsJsonAsync(
            "/api/vertex/message",
            new { messageName = $"wrong-{messageName}", processInstanceId = started.Id.ToString(), variables = new { ignored = true } },
            TestContext.Current.CancellationToken);
        wrongCorrelation.EnsureSuccessStatusCode();
        Assert.Equal(ProcessInstanceStatus.Running, (await GetInstanceAsync(started.Id)).Status);

        using var correlation = await _client.PostAsJsonAsync(
            "/api/vertex/message",
            new { messageName, processInstanceId = started.Id.ToString(), variables = new { correlated = true } },
            TestContext.Current.CancellationToken);
        correlation.EnsureSuccessStatusCode();
        var result = await correlation.Content.ReadFromJsonAsync<MessageCorrelation>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(started.Id.ToString(), result.ProcessInstanceId);

        var completed = await GetInstanceAsync(started.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Empty(completed.ActiveTokens);
    }

    [Fact]
    public async Task P1_AC_03B_Signal_Waits_Broadcasts_To_Matching_Subscriptions_And_Resumes_Once()
    {
        var processKey = $"phase1-signal-{Guid.NewGuid():N}";
        var signalName = $"release-approved-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <signal id="release-signal" name="{{signalName}}" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-signal" sourceRef="start" targetRef="await-signal" />
                <intermediateCatchEvent id="await-signal" name="Await release">
                  <signalEventDefinition signalRef="release-signal" />
                </intermediateCatchEvent>
                <sequenceFlow id="to-end" sourceRef="await-signal" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, first) = await DeployAndStartAsync(processKey, bpmn);
        var second = await StartAsync(processKey, new Dictionary<string, object> { ["instance"] = "second" });
        Assert.True(
            first.ActiveTokens.Contains("await-signal") && second.ActiveTokens.Contains("await-signal"),
            "Signal subscription wait tokens were not persisted for both process instances.");

        using var broadcast = await _client.PostAsJsonAsync(
            "/api/vertex/signal",
            new { signalName, variables = new { released = true } },
            TestContext.Current.CancellationToken);
        broadcast.EnsureSuccessStatusCode();

        var firstCompleted = await GetInstanceAsync(first.Id);
        var secondCompleted = await GetInstanceAsync(second.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, firstCompleted.Status);
        Assert.Equal(ProcessInstanceStatus.Completed, secondCompleted.Status);
        Assert.Empty(firstCompleted.ActiveTokens);
        Assert.Empty(secondCompleted.ActiveTokens);
    }

    [Fact]
    public async Task P1_AC_04_Host_Restart_Preserves_Wait_State_And_Resumes_Without_Duplication()
    {
        var processKey = $"phase1-restart-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-approval" sourceRef="start" targetRef="approval" />
                <userTask id="approval" name="Persistent approval" />
                <sequenceFlow id="to-end" sourceRef="approval" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        var originalTasks = await GetTasksAsync(started.Id);
        Assert.True(
            originalTasks.Count == 1,
            "Restart precondition failed: the durable user-task wait state was not persisted.");
        var originalTask = originalTasks[0];
        Assert.Contains("approval", started.ActiveTokens);

        _client.Dispose();
        _factory.Dispose();

        using var restartedFactory = new CustomWebApplicationFactory().WithPersistentBpmnDatabase(_bpmnDatabase);
        using var restartedClient = restartedFactory.CreateClient(_output);
        var restored = await restartedClient.GetFromJsonAsync<RuntimeInstance>(
            $"/api/runtime/{started.Id}",
            TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(ProcessInstanceStatus.Running, restored.Status);
        Assert.Contains("approval", restored.ActiveTokens);

        var restoredTasks = await restartedClient.GetFromJsonAsync<List<PersistedUserTask>>(
            $"/api/task?processInstanceId={started.Id}",
            TestContext.Current.CancellationToken);
        Assert.Equal(originalTask.Id, Assert.Single(restoredTasks ?? []).Id);

        using var complete = await restartedClient.PostAsJsonAsync(
            $"/api/task/{originalTask.Id}/complete",
            new { variables = new { approved = true }, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, complete.StatusCode);

        var completed = await restartedClient.GetFromJsonAsync<RuntimeInstance>(
            $"/api/runtime/{started.Id}",
            TestContext.Current.CancellationToken);
        Assert.NotNull(completed);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Empty(completed.ActiveTasks);
        Assert.Empty(completed.ActiveTokens);
    }

    [Fact]
    public async Task P1_AC_05_Parallel_Join_Waits_For_Both_Branches_And_Instances_Are_Isolated()
    {
        var processKey = $"phase1-parallel-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-split" sourceRef="start" targetRef="split" />
                <parallelGateway id="split" />
                <sequenceFlow id="to-legal" sourceRef="split" targetRef="legal" />
                <sequenceFlow id="to-risk" sourceRef="split" targetRef="risk" />
                <userTask id="legal" name="Legal review" />
                <userTask id="risk" name="Risk review" />
                <sequenceFlow id="legal-to-join" sourceRef="legal" targetRef="join" />
                <sequenceFlow id="risk-to-join" sourceRef="risk" targetRef="join" />
                <parallelGateway id="join" />
                <sequenceFlow id="to-end" sourceRef="join" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, first) = await DeployAndStartAsync(processKey, bpmn);
        var second = await StartAsync(processKey, new Dictionary<string, object> { ["instance"] = "second" });
        var firstTasks = await GetTasksAsync(first.Id);
        var secondTasks = await GetTasksAsync(second.Id);

        Assert.True(
            firstTasks.Count == 2 && secondTasks.Count == 2,
            "Parallel split did not persist two isolated user-task branches per process instance.");
        Assert.Equal(["Legal review", "Risk review"], firstTasks.Select(task => task.Name).Order().ToArray());
        Assert.Equal(["Legal review", "Risk review"], secondTasks.Select(task => task.Name).Order().ToArray());
        Assert.DoesNotContain(firstTasks, firstTask => secondTasks.Any(secondTask => secondTask.Id == firstTask.Id));

        await CompleteTaskAsync(firstTasks[0].Id);
        Assert.Equal(ProcessInstanceStatus.Running, (await GetInstanceAsync(first.Id)).Status);
        Assert.Equal(ProcessInstanceStatus.Running, (await GetInstanceAsync(second.Id)).Status);

        await CompleteTaskAsync(firstTasks[1].Id);
        var firstCompleted = await GetInstanceAsync(first.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, firstCompleted.Status);
        Assert.Empty(firstCompleted.ActiveTokens);
        Assert.Equal(ProcessInstanceStatus.Running, (await GetInstanceAsync(second.Id)).Status);

        await CompleteTaskAsync(secondTasks[0].Id);
        Assert.Equal(ProcessInstanceStatus.Running, (await GetInstanceAsync(second.Id)).Status);
        await CompleteTaskAsync(secondTasks[1].Id);
        var secondCompleted = await GetInstanceAsync(second.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, secondCompleted.Status);
        Assert.Empty(secondCompleted.ActiveTokens);
    }

    private async Task<(DeployedProcess Definition, RuntimeInstance Instance)> DeployAndStartAsync(
        string processKey,
        string bpmn)
    {
        using var deployResponse = await _client.PostAsJsonAsync(
            "/api/repository",
            new { bpmnXml = bpmn, name = $"{processKey}.bpmn", tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        deployResponse.EnsureSuccessStatusCode();
        var definition = await deployResponse.Content.ReadFromJsonAsync<DeployedProcess>(TestContext.Current.CancellationToken);
        Assert.NotNull(definition);
        Assert.Equal(processKey, definition.Key);

        return (definition, await StartAsync(processKey));
    }

    private async Task<RuntimeInstance> StartAsync(
        string processKey,
        Dictionary<string, object>? variables = null)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/runtime/start",
            new
            {
                processDefinitionKey = processKey,
                variables = variables ?? new Dictionary<string, object>(),
                businessKey = $"acceptance-{Guid.NewGuid():N}",
                tenantId = (string?)null
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var instance = await response.Content.ReadFromJsonAsync<RuntimeInstance>(TestContext.Current.CancellationToken);
        Assert.NotNull(instance);
        return instance;
    }

    private async Task<RuntimeInstance> GetInstanceAsync(Guid id)
    {
        var instance = await _client.GetFromJsonAsync<RuntimeInstance>(
            $"/api/runtime/{id}",
            TestContext.Current.CancellationToken);
        Assert.NotNull(instance);
        return instance;
    }

    private async Task<List<PersistedUserTask>> GetTasksAsync(Guid processInstanceId) =>
        await _client.GetFromJsonAsync<List<PersistedUserTask>>(
            $"/api/task?processInstanceId={processInstanceId}",
            TestContext.Current.CancellationToken) ?? [];

    private async Task CompleteTaskAsync(Guid taskId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/task/{taskId}/complete",
            new { variables = new { approved = true }, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<RuntimeInstance> WaitForStatusAsync(
        Guid processInstanceId,
        ProcessInstanceStatus status,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        RuntimeInstance instance;
        do
        {
            instance = await GetInstanceAsync(processInstanceId);
            if (instance.Status == status)
                return instance;
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Equal(status, instance.Status);
        return instance;
    }

    private sealed record DeployedProcess(Guid Id, string Key, string Name, int Version);

    private sealed record RuntimeInstance(
        Guid Id,
        Guid ProcessDefinitionId,
        ProcessInstanceStatus Status,
        DateTime? EndedAt,
        IReadOnlyList<string> ActiveTasks,
        IReadOnlyList<string> ActiveTokens,
        Dictionary<string, JsonElement> Variables);

    private sealed record PersistedUserTask(Guid Id, string Name, UserTaskStatus Status);

    private sealed record PersistedJob(string Id, string ProcessInstanceId, string JobType, DateTime DueDate);

    private sealed record MessageCorrelation(string ResultType, string ExecutionId, string ProcessInstanceId, string ProcessDefinitionId);
}
