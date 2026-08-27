using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "Phase1Acceptance")]
[Trait("Category", "FullProductSupportAcceptance")]
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
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"phase1_{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30,
            Pooling = false
        }.ToString();
        _bpmnDatabase = new SqliteConnection(connectionString);
        _bpmnDatabase.Open();
        _factory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(connectionString)
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
    public async Task FPS_BPMN_07_NonInterrupting_BoundaryTimer_Leaves_Attached_Task_Active()
    {
        var processKey = $"fps-noninterrupting-timer-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-review" sourceRef="start" targetRef="review" />
                <userTask id="review" name="Review remains active" />
                <boundaryEvent id="reminder" attachedToRef="review" cancelActivity="false">
                  <timerEventDefinition><timeDuration>PT0.25S</timeDuration></timerEventDefinition>
                </boundaryEvent>
                <sequenceFlow id="to-reminder-end" sourceRef="reminder" targetRef="reminder-end" />
                <endEvent id="reminder-end" />
                <sequenceFlow id="to-main-end" sourceRef="review" targetRef="main-end" />
                <endEvent id="main-end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        var review = Assert.Single(await GetTasksAsync(started.Id));

        await WaitForNoActiveJobsAsync(started.Id, TimeSpan.FromSeconds(8));

        var afterReminder = await GetInstanceAsync(started.Id);
        Assert.Equal(ProcessInstanceStatus.Running, afterReminder.Status);
        Assert.Contains("review", afterReminder.ActiveTokens);
        Assert.Equal(UserTaskStatus.Pending, Assert.Single(await GetTasksAsync(started.Id)).Status);

        await CompleteTaskAsync(review.Id);
        var completed = await GetInstanceAsync(started.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Empty(completed.ActiveTokens);
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

        using var restartedFactory = new CustomWebApplicationFactory().WithPersistentBpmnDatabase(_bpmnDatabase.ConnectionString);
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
    public async Task P4_AC_08_CMMN_Restart_Preserves_Case_And_Discretionary_Item_Lifecycle()
    {
        var key = $"cmmn-restart-{Guid.NewGuid():N}";
        var cmmn = $$"""
            <definitions xmlns="https://www.omg.org/spec/CMMN/20151109/MODEL">
              <case id="{{key}}" name="Restart case">
                <casePlanModel id="plan">
                  <planItem id="required-review" definitionRef="requiredDefinition" />
                  <planningTable>
                    <discretionaryItem id="optional-review" definitionRef="optionalDefinition" />
                  </planningTable>
                  <humanTask id="requiredDefinition" />
                  <manualTask id="optionalDefinition" />
                </casePlanModel>
              </case>
            </definitions>
            """;
        using var deploy = await _client.PostAsJsonAsync("/api/case-definitions/deploy", new
        {
            key,
            name = "Restart case",
            cmmnXml = cmmn
        }, TestContext.Current.CancellationToken);
        deploy.EnsureSuccessStatusCode();
        using var start = await _client.PostAsJsonAsync(
            $"/api/case-definitions/{key}/start",
            new { },
            TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        using var startPayload = JsonDocument.Parse(await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var instanceId = startPayload.RootElement.GetProperty("caseInstanceId").GetGuid();

        _client.Dispose();
        _factory.Dispose();

        using var restartedFactory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(_bpmnDatabase.ConnectionString);
        using var restartedClient = restartedFactory.CreateClient(_output);
        using var restored = await restartedClient.GetAsync(
            $"/api/case-definitions/instances/{instanceId}",
            TestContext.Current.CancellationToken);
        restored.EnsureSuccessStatusCode();
        using var restoredPayload = JsonDocument.Parse(await restored.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Active", restoredPayload.RootElement.GetProperty("state").GetString());

        using var activate = await restartedClient.PostAsJsonAsync(
            $"/api/case-definitions/instances/{instanceId}/discretionary-items/optional-review/activate",
            new { },
            TestContext.Current.CancellationToken);
        activate.EnsureSuccessStatusCode();
        using var activatePayload = JsonDocument.Parse(await activate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("PLAN_ITEM_ACTIVE:optional-review:manualtask",
            activatePayload.RootElement.GetProperty("trace").EnumerateArray().Select(item => item.GetString()));

        foreach (var planItem in new[] { "optional-review", "required-review" })
        {
            using var complete = await restartedClient.PostAsJsonAsync(
                $"/api/case-definitions/instances/{instanceId}/plan-items/{planItem}/complete",
                new { },
                TestContext.Current.CancellationToken);
            complete.EnsureSuccessStatusCode();
            if (planItem == "required-review")
            {
                using var payload = JsonDocument.Parse(await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Equal("Completed", payload.RootElement.GetProperty("state").GetString());
            }
        }
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

    [Fact]
    [Trait("Category", "FullProductSupportAcceptance")]
    public async Task FPS_BPMN_01_ExclusiveGateway_Evaluates_Conditions_And_DefaultFlow()
    {
        var processKey = $"full-support-exclusive-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-decision" sourceRef="start" targetRef="decision" />
                <exclusiveGateway id="decision" default="to-manual" />
                <sequenceFlow id="to-approved" sourceRef="decision" targetRef="approved">
                  <conditionExpression>${riskScore &gt;= 700}</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="to-manual" sourceRef="decision" targetRef="manual" />
                <userTask id="approved" name="Automatically approved" />
                <userTask id="manual" name="Manual review" />
                <sequenceFlow id="approved-end" sourceRef="approved" targetRef="end" />
                <sequenceFlow id="manual-end" sourceRef="manual" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, conditionalInstance) = await DeployAndStartAsync(
            processKey,
            bpmn,
            new Dictionary<string, object> { ["riskScore"] = 750 });
        var conditional = await GetTasksAsync(conditionalInstance.Id);
        Assert.Equal("Automatically approved", Assert.Single(conditional).Name);

        var fallback = await StartAsync(processKey, new Dictionary<string, object> { ["riskScore"] = 699 });
        var fallbackTasks = await GetTasksAsync(fallback.Id);
        Assert.Equal("Manual review", Assert.Single(fallbackTasks).Name);
    }

    [Fact]
    [Trait("Category", "FullProductSupportAcceptance")]
    public async Task FPS_BPMN_02_InclusiveGateway_Activates_All_Matches_And_Uses_Default_Only_Without_Match()
    {
        var processKey = $"full-support-inclusive-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-routing" sourceRef="start" targetRef="routing" />
                <inclusiveGateway id="routing" default="to-standard" />
                <sequenceFlow id="to-email" sourceRef="routing" targetRef="email">
                  <conditionExpression>${emailRequested == true}</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="to-sms" sourceRef="routing" targetRef="sms">
                  <conditionExpression>${smsRequested == true}</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="to-standard" sourceRef="routing" targetRef="standard" />
                <userTask id="email" name="Send email" />
                <userTask id="sms" name="Send SMS" />
                <userTask id="standard" name="Standard notification" />
              </process>
            </definitions>
            """;

        var (_, matched) = await DeployAndStartAsync(processKey, bpmn, new Dictionary<string, object>
        {
            ["emailRequested"] = true,
            ["smsRequested"] = true
        });
        Assert.Equal(["Send email", "Send SMS"], (await GetTasksAsync(matched.Id)).Select(task => task.Name).Order().ToArray());

        var fallback = await StartAsync(processKey, new Dictionary<string, object>
        {
            ["emailRequested"] = false,
            ["smsRequested"] = false
        });
        Assert.Equal("Standard notification", Assert.Single(await GetTasksAsync(fallback.Id)).Name);
    }

    [Fact]
    public async Task FPS_BPMN_03_EventBasedGateway_Consumes_One_Branch_And_Cancels_Competitors()
    {
        var processKey = $"full-support-event-gateway-{Guid.NewGuid():N}";
        var messageName = $"event-winner-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <message id="winner-message" name="{{messageName}}" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-race" sourceRef="start" targetRef="race" />
                <eventBasedGateway id="race" />
                <sequenceFlow id="to-message" sourceRef="race" targetRef="message" />
                <sequenceFlow id="to-timeout" sourceRef="race" targetRef="timeout" />
                <intermediateCatchEvent id="message">
                  <messageEventDefinition messageRef="winner-message" />
                </intermediateCatchEvent>
                <intermediateCatchEvent id="timeout">
                  <timerEventDefinition><timeDuration>PT10M</timeDuration></timerEventDefinition>
                </intermediateCatchEvent>
                <sequenceFlow id="message-end" sourceRef="message" targetRef="end" />
                <sequenceFlow id="timeout-end" sourceRef="timeout" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        Assert.Equal(["message", "timeout"], started.ActiveTokens.Order().ToArray());

        using var correlation = await _client.PostAsJsonAsync(
            "/api/vertex/message",
            new { messageName, processInstanceId = started.Id.ToString(), variables = new { winner = "message" } },
            TestContext.Current.CancellationToken);
        correlation.EnsureSuccessStatusCode();

        var completed = await GetInstanceAsync(started.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Empty(completed.ActiveTokens);

        using var duplicate = await _client.PostAsJsonAsync(
            "/api/vertex/message",
            new { messageName, processInstanceId = started.Id.ToString(), variables = new { } },
            TestContext.Current.CancellationToken);
        duplicate.EnsureSuccessStatusCode();
        var duplicateResult = await duplicate.Content.ReadFromJsonAsync<MessageCorrelation>(TestContext.Current.CancellationToken);
        Assert.Equal("not_found", duplicateResult?.ResultType);
    }

    [Fact]
    public async Task FPS_BPMN_04_ComplexGateway_Activates_All_Conditionally_Eligible_Flows()
    {
        var processKey = $"full-support-complex-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-complex" sourceRef="start" targetRef="complex" />
                <complexGateway id="complex" default="to-manual" />
                <sequenceFlow id="to-fraud" sourceRef="complex" targetRef="fraud">
                  <conditionExpression>${fraudScore &gt; 80}</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="to-sanctions" sourceRef="complex" targetRef="sanctions">
                  <conditionExpression>${sanctionsHit == true}</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="to-manual" sourceRef="complex" targetRef="manual" />
                <userTask id="fraud" name="Fraud review" />
                <userTask id="sanctions" name="Sanctions review" />
                <userTask id="manual" name="Manual review" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn, new Dictionary<string, object>
        {
            ["fraudScore"] = 91,
            ["sanctionsHit"] = true
        });
        Assert.Equal(
            ["Fraud review", "Sanctions review"],
            (await GetTasksAsync(started.Id)).Select(task => task.Name).Order().ToArray());
    }

    [Fact]
    public async Task FPS_BPMN_05_ErrorEndEvent_Interrupts_Subprocess_And_Uses_Matching_Boundary()
    {
        var processKey = $"full-support-error-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <error id="validation-error" errorCode="VALIDATION" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-scope" sourceRef="start" targetRef="scope" />
                <subProcess id="scope">
                  <startEvent id="scope-start" />
                  <sequenceFlow id="to-error" sourceRef="scope-start" targetRef="error-end" />
                  <endEvent id="error-end">
                    <errorEventDefinition errorRef="validation-error" />
                  </endEvent>
                </subProcess>
                <boundaryEvent id="error-boundary" attachedToRef="scope" cancelActivity="true">
                  <errorEventDefinition errorRef="validation-error" />
                </boundaryEvent>
                <sequenceFlow id="normal-path" sourceRef="scope" targetRef="normal" />
                <sequenceFlow id="error-path" sourceRef="error-boundary" targetRef="recovery" />
                <userTask id="normal" name="Normal continuation" />
                <userTask id="recovery" name="Recover validation error" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        var task = Assert.Single(await GetTasksAsync(started.Id));
        Assert.Equal("Recover validation error", task.Name);
    }

    [Fact]
    public async Task FPS_BPMN_10_Parallel_MultiInstance_UserTask_Uses_Collection_And_Joins_All_Instances()
    {
        var processKey = $"fps-mi-parallel-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-review" sourceRef="start" targetRef="review" />
                <userTask id="review" name="Review item">
                  <multiInstanceLoopCharacteristics camunda:collection="reviewers" camunda:elementVariable="reviewer" />
                </userTask>
                <sequenceFlow id="to-end" sourceRef="review" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn, new Dictionary<string, object>
        {
            ["reviewers"] = new[] { "Ada", "Grace", "Linus" }
        });
        var tasks = await GetTasksAsync(started.Id);
        Assert.Equal(3, tasks.Count);
        Assert.Equal(
            ["Ada", "Grace", "Linus"],
            tasks.Select(task => task.LocalVariables["reviewer"].GetString()).Order().ToArray());
        Assert.Single(tasks.Select(task => task.MultiInstanceExecutionId).Distinct());

        await CompleteTaskAsync(tasks[0].Id);
        await CompleteTaskAsync(tasks[1].Id);
        Assert.Equal(ProcessInstanceStatus.Running, (await GetInstanceAsync(started.Id)).Status);

        await CompleteTaskAsync(tasks[2].Id);
        Assert.Equal(ProcessInstanceStatus.Completed, (await GetInstanceAsync(started.Id)).Status);
    }

    [Fact]
    public async Task FPS_BPMN_11_Sequential_MultiInstance_Stops_When_CompletionCondition_Is_True()
    {
        var processKey = $"fps-mi-sequential-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-review" sourceRef="start" targetRef="review" />
                <userTask id="review" name="Sequential review">
                  <multiInstanceLoopCharacteristics isSequential="true">
                    <loopCardinality>4</loopCardinality>
                    <completionCondition>${nrOfCompletedInstances >= 2}</completionCondition>
                  </multiInstanceLoopCharacteristics>
                </userTask>
                <sequenceFlow id="to-end" sourceRef="review" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        var first = Assert.Single(await GetTasksAsync(started.Id));
        Assert.Equal(0, first.MultiInstanceIndex);

        await CompleteTaskAsync(first.Id);
        var second = Assert.Single(await GetTasksAsync(started.Id));
        Assert.Equal(1, second.MultiInstanceIndex);

        await CompleteTaskAsync(second.Id);
        var completed = await GetInstanceAsync(started.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Empty(await GetTasksAsync(started.Id));
    }

    [Fact]
    public async Task FPS_BPMN_08_NonInterrupting_Escalation_Boundary_Preserves_Subprocess_Path()
    {
        var processKey = $"fps-escalation-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <escalation id="urgent" escalationCode="URGENT" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-scope" sourceRef="start" targetRef="scope" />
                <subProcess id="scope">
                  <startEvent id="inner-start" />
                  <sequenceFlow id="to-escalate" sourceRef="inner-start" targetRef="escalate" />
                  <intermediateThrowEvent id="escalate"><escalationEventDefinition escalationRef="urgent" /></intermediateThrowEvent>
                  <sequenceFlow id="to-inner-review" sourceRef="escalate" targetRef="inner-review" />
                  <userTask id="inner-review" name="Inner review" />
                  <sequenceFlow id="to-inner-end" sourceRef="inner-review" targetRef="inner-end" />
                  <endEvent id="inner-end" />
                </subProcess>
                <boundaryEvent id="escalation-handler" attachedToRef="scope" cancelActivity="false">
                  <escalationEventDefinition escalationRef="urgent" />
                </boundaryEvent>
                <sequenceFlow id="to-escalation-review" sourceRef="escalation-handler" targetRef="escalation-review" />
                <userTask id="escalation-review" name="Escalation review" />
                <sequenceFlow id="escalation-to-end" sourceRef="escalation-review" targetRef="end" />
                <sequenceFlow id="scope-to-end" sourceRef="scope" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        var tasks = await GetTasksAsync(started.Id);
        Assert.Equal(["Escalation review", "Inner review"], tasks.Select(task => task.Name).Order().ToArray());

        foreach (var task in tasks)
            await CompleteTaskAsync(task.Id);

        Assert.Equal(ProcessInstanceStatus.Completed, (await GetInstanceAsync(started.Id)).Status);
    }

    [Fact]
    public async Task FPS_BPMN_09_CancelEndEvent_Interrupts_Transaction_And_Uses_CancelBoundary()
    {
        var processKey = $"fps-cancel-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-transaction" sourceRef="start" targetRef="payment" />
                <transaction id="payment">
                  <startEvent id="payment-start" />
                  <sequenceFlow id="to-cancel" sourceRef="payment-start" targetRef="cancel" />
                  <endEvent id="cancel"><cancelEventDefinition /></endEvent>
                </transaction>
                <boundaryEvent id="cancel-handler" attachedToRef="payment"><cancelEventDefinition /></boundaryEvent>
                <sequenceFlow id="to-resolution" sourceRef="cancel-handler" targetRef="resolution" />
                <userTask id="resolution" name="Manual resolution" />
                <sequenceFlow id="resolution-to-end" sourceRef="resolution" targetRef="end" />
                <sequenceFlow id="normal-to-end" sourceRef="payment" targetRef="normal-end" />
                <endEvent id="normal-end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;

        var (_, started) = await DeployAndStartAsync(processKey, bpmn);
        var resolution = Assert.Single(await GetTasksAsync(started.Id));
        Assert.Equal("Manual resolution", resolution.Name);

        await CompleteTaskAsync(resolution.Id);

        Assert.Equal(ProcessInstanceStatus.Completed, (await GetInstanceAsync(started.Id)).Status);
    }

    [Fact]
    public async Task FPS_BPMN_06_TerminateEndEvent_Cancels_All_Other_Process_Branches()
    {
        var processKey = $"full-support-terminate-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         targetNamespace="https://vertexbpmn.dev/acceptance">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-split" sourceRef="start" targetRef="split" />
                <parallelGateway id="split" />
                <sequenceFlow id="terminate-branch" sourceRef="split" targetRef="terminate" />
                <sequenceFlow id="task-branch" sourceRef="split" targetRef="must-cancel" />
                <endEvent id="terminate"><terminateEventDefinition /></endEvent>
                <userTask id="must-cancel" name="Must never remain active" />
              </process>
            </definitions>
            """;

        var (_, completed) = await DeployAndStartAsync(processKey, bpmn);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Empty(completed.ActiveTasks);
        Assert.Empty(completed.ActiveTokens);
        Assert.Empty(await GetTasksAsync(completed.Id));
    }

    private async Task<(DeployedProcess Definition, RuntimeInstance Instance)> DeployAndStartAsync(
        string processKey,
        string bpmn,
        Dictionary<string, object>? variables = null)
    {
        using var deployResponse = await _client.PostAsJsonAsync(
            "/api/repository",
            new { bpmnXml = bpmn, name = $"{processKey}.bpmn", tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        deployResponse.EnsureSuccessStatusCode();
        var definition = await deployResponse.Content.ReadFromJsonAsync<DeployedProcess>(TestContext.Current.CancellationToken);
        Assert.NotNull(definition);
        Assert.Equal(processKey, definition.Key);

        return (definition, await StartAsync(processKey, variables));
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

    private async Task WaitForNoActiveJobsAsync(Guid processInstanceId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            var jobs = await _client.GetFromJsonAsync<List<PersistedJob>>(
                "/api/vertex/job",
                TestContext.Current.CancellationToken) ?? [];
            if (jobs.All(job => job.ProcessInstanceId != processInstanceId.ToString()))
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        var remaining = await _client.GetFromJsonAsync<List<PersistedJob>>(
            "/api/vertex/job",
            TestContext.Current.CancellationToken) ?? [];
        Assert.DoesNotContain(remaining, job => job.ProcessInstanceId == processInstanceId.ToString());
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

    private sealed record PersistedUserTask(
        Guid Id,
        string Name,
        UserTaskStatus Status,
        Guid? MultiInstanceExecutionId,
        int? MultiInstanceIndex,
        Dictionary<string, JsonElement> LocalVariables);

    private sealed record PersistedJob(string Id, string ProcessInstanceId, string JobType, DateTime DueDate);

    private sealed record MessageCorrelation(string ResultType, string ExecutionId, string ProcessInstanceId, string ProcessDefinitionId);
}
