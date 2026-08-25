using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "Phase2Acceptance")]
public sealed class PersistentRuntimePhase2AcceptanceTests : IDisposable
{
    private readonly SqliteConnection _database;
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PersistentRuntimePhase2AcceptanceTests(CustomWebApplicationFactory _, ITestOutputHelper output)
    {
        _database = new SqliteConnection($"Data Source=file:phase2_{Guid.NewGuid():N}?mode=memory&cache=shared");
        _database.Open();
        _factory = new CustomWebApplicationFactory().WithPersistentBpmnDatabase(_database.ConnectionString);
        _client = _factory.CreateClient(output);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task P2_AC_01_Start_is_idempotent_and_does_not_create_duplicate_instances()
    {
        var key = $"phase2-idempotent-{Guid.NewGuid():N}";
        await DeployAsync(key, SimpleProcess(key), null);
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var starts = await Task.WhenAll(
            StartAsync(key, null, idempotencyKey),
            StartAsync(key, null, idempotencyKey));
        var first = starts[0];
        var second = starts[1];

        Assert.Equal(first.Id, second.Id);
        var instances = await _client.GetFromJsonAsync<List<RuntimeInstance>>(
            "/api/runtime", TestContext.Current.CancellationToken);
        Assert.Single((instances ?? []).Where(instance => instance.Id == first.Id));
    }

    [Fact]
    public async Task P2_AC_02_Signal_broadcast_is_tenant_isolated()
    {
        var key = $"phase2-tenant-{Guid.NewGuid():N}";
        var signal = $"signal-{Guid.NewGuid():N}";
        var bpmn = SignalProcess(key, signal);
        await DeployAsync(key, bpmn, "tenant-a");
        await DeployAsync(key, bpmn, "tenant-b");
        var first = await StartAsync(key, "tenant-a");
        var second = await StartAsync(key, "tenant-b");

        using var response = await _client.PostAsJsonAsync(
            "/api/vertex/signal",
            new { signalName = signal, variables = new { released = true }, tenantId = "tenant-a" },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.Equal(ProcessInstanceStatus.Completed, (await GetAsync(first.Id)).Status);
        Assert.Equal(ProcessInstanceStatus.Running, (await GetAsync(second.Id)).Status);
    }

    [Fact]
    public async Task P2_AC_03_Compensation_subscription_survives_wait_and_runs_handler()
    {
        var key = $"phase2-compensation-{Guid.NewGuid():N}";
        var bpmn = CompensationProcess(key);
        await DeployAsync(key, bpmn, null);

        var instance = await StartAsync(key, null);
        Assert.Equal(ProcessInstanceStatus.Running, instance.Status);
        var tasks = await _client.GetFromJsonAsync<List<PersistedTask>>(
            $"/api/task?processInstanceId={instance.Id}", TestContext.Current.CancellationToken);
        var compensationTask = Assert.Single(tasks ?? []);
        Assert.Equal("Undo score", compensationTask.Name);

        using var complete = await _client.PostAsJsonAsync(
            $"/api/task/{compensationTask.Id}/complete",
            new { variables = new { compensated = true }, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, complete.StatusCode);
        Assert.Equal(ProcessInstanceStatus.Completed, (await GetAsync(instance.Id)).Status);
    }

    [Fact]
    public async Task P2_AC_04_Transient_service_failure_creates_incident_and_recovery_resumes_exactly_once()
    {
        var handler = new FailOnceServiceTaskHandler();
        using var database = new SqliteConnection($"Data Source=file:phase2_recovery_{Guid.NewGuid():N}?mode=memory&cache=shared");
        database.Open();
        using var factory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(database.ConnectionString)
            .WithTestServices(services =>
            {
                services.RemoveAll<IServiceTaskRegistry>();
                services.AddSingleton<IServiceTaskRegistry>(_ =>
                {
                    var registry = new ServiceTaskRegistry();
                    registry.Register("phase2:fail-once", handler);
                    return registry;
                });
            });
        using var client = factory.CreateClient();
        var key = $"phase2-recovery-{Guid.NewGuid():N}";
        using (var deployment = await client.PostAsJsonAsync(
                   "/api/repository",
                   new { bpmnXml = FailingServiceProcess(key), name = $"{key}.bpmn", tenantId = (string?)null },
                   TestContext.Current.CancellationToken))
            deployment.EnsureSuccessStatusCode();

        using var start = await client.PostAsJsonAsync(
            "/api/runtime/start",
            new { processDefinitionKey = key, variables = new { }, businessKey = Guid.NewGuid().ToString("N"), tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        var instance = (await start.Content.ReadFromJsonAsync<RuntimeInstance>(TestContext.Current.CancellationToken))!;
        Assert.Equal(ProcessInstanceStatus.Suspended, instance.Status);

        var incidents = await client.GetFromJsonAsync<List<IncidentRecord>>(
            "/api/vertex/incident", TestContext.Current.CancellationToken);
        var incident = Assert.Single((incidents ?? []).Where(item => item.ProcessInstanceId == instance.Id.ToString()));
        Assert.Equal("ExecutionFailure", incident.IncidentType);

        using var recovery = new HttpRequestMessage(HttpMethod.Post, $"/api/vertex/incident/{incident.Id}/resolve")
        {
            Content = JsonContent.Create(new { tenantId = (string?)null })
        };
        recovery.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var recovered = await client.SendAsync(recovery, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, recovered.StatusCode);
        Assert.Equal(ProcessInstanceStatus.Completed,
            (await client.GetFromJsonAsync<RuntimeInstance>($"/api/runtime/{instance.Id}", TestContext.Current.CancellationToken))!.Status);
        Assert.Equal(2, handler.Attempts);
    }

    [Fact]
    public async Task P2_AC_05_Runtime_read_is_tenant_authorized()
    {
        var key = $"phase2-auth-{Guid.NewGuid():N}";
        await DeployAsync(key, SignalProcess(key, $"signal-{Guid.NewGuid():N}"), "tenant-a");
        var instance = await StartAsync(key, "tenant-a");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/runtime/{instance.Id}");
        request.Headers.Add("X-Test-User", "tenant-b-user");
        request.Headers.Add("X-Test-Tenant", "tenant-b");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task P2_AC_06_Two_api_replicas_share_idempotency_claim_without_duplicate_execution()
    {
        using var database = new SqliteConnection($"Data Source=file:phase2_replicas_{Guid.NewGuid():N}?mode=memory&cache=shared");
        database.Open();
        using var firstFactory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(database.ConnectionString);
        using var secondFactory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(database.ConnectionString);
        using var firstClient = firstFactory.CreateClient();
        using var secondClient = secondFactory.CreateClient();
        var key = $"phase2-replicas-{Guid.NewGuid():N}";
        using (var deployment = await firstClient.PostAsJsonAsync(
                   "/api/repository",
                   new { bpmnXml = SimpleProcess(key), name = $"{key}.bpmn", tenantId = (string?)null },
                   TestContext.Current.CancellationToken))
            deployment.EnsureSuccessStatusCode();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var starts = await Task.WhenAll(
            StartWithClientAsync(firstClient, key, null, idempotencyKey),
            StartWithClientAsync(secondClient, key, null, idempotencyKey));

        Assert.Equal(starts[0].Id, starts[1].Id);
        var instances = await firstClient.GetFromJsonAsync<List<RuntimeInstance>>(
            "/api/runtime", TestContext.Current.CancellationToken);
        Assert.Single((instances ?? []).Where(instance => instance.Id == starts[0].Id));
    }

    private async Task DeployAsync(string key, string bpmn, string? tenantId)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/repository", new { bpmnXml = bpmn, name = $"{key}.bpmn", tenantId },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<RuntimeInstance> StartAsync(string key, string? tenantId, string? idempotencyKey = null)
        => await StartWithClientAsync(_client, key, tenantId, idempotencyKey);

    private static async Task<RuntimeInstance> StartWithClientAsync(
        HttpClient client,
        string key,
        string? tenantId,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/runtime/start")
        {
            Content = JsonContent.Create(new
            {
                processDefinitionKey = key,
                variables = new Dictionary<string, object>
                {
                    ["applicantName"] = "Phase Two",
                    ["age"] = 42
                },
                businessKey = Guid.NewGuid().ToString("N"),
                tenantId
            })
        };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RuntimeInstance>(TestContext.Current.CancellationToken))!;
    }

    private async Task<RuntimeInstance> GetAsync(Guid id) =>
        (await _client.GetFromJsonAsync<RuntimeInstance>(
            $"/api/runtime/{id}", TestContext.Current.CancellationToken))!;

    private static string SimpleProcess(string key) => $$"""
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/phase2">
          <process id="{{key}}" isExecutable="true">
            <startEvent id="start"/><sequenceFlow id="flow" sourceRef="start" targetRef="end"/><endEvent id="end"/>
          </process>
        </definitions>
        """;

    private static string FailingServiceProcess(string key) => $$"""
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/phase2">
          <process id="{{key}}" isExecutable="true">
            <startEvent id="start"/>
            <sequenceFlow id="to-service" sourceRef="start" targetRef="unstable"/>
            <serviceTask id="unstable" name="Transient call" implementation="phase2:fail-once"/>
            <sequenceFlow id="to-end" sourceRef="unstable" targetRef="end"/>
            <endEvent id="end"/>
          </process>
        </definitions>
        """;

    private sealed class FailOnceServiceTaskHandler : IServiceTaskHandler
    {
        private int _attempts;
        public int Attempts => _attempts;

        public Task ExecuteAsync(
            IDictionary<string, string> attributes,
            IDictionary<string, object> variables,
            CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
                throw new HttpRequestException("Temporary upstream failure.");
            variables["recovered"] = true;
            return Task.CompletedTask;
        }
    }

    private sealed record IncidentRecord(string Id, string ProcessInstanceId, string IncidentType);

    private static string SignalProcess(string key, string signal) => $$"""
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/phase2">
          <signal id="signal-definition" name="{{signal}}"/>
          <process id="{{key}}" isExecutable="true">
            <startEvent id="start"/><sequenceFlow id="to-wait" sourceRef="start" targetRef="wait"/>
            <intermediateCatchEvent id="wait"><signalEventDefinition signalRef="signal-definition"/></intermediateCatchEvent>
            <sequenceFlow id="to-end" sourceRef="wait" targetRef="end"/><endEvent id="end"/>
          </process>
        </definitions>
        """;

    private static string CompensationProcess(string key) => $$"""
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/phase2">
          <process id="{{key}}" isExecutable="true">
            <startEvent id="start"/><sequenceFlow id="to-score" sourceRef="start" targetRef="score"/>
            <serviceTask id="score" implementation="calculateScore"/>
            <boundaryEvent id="compensate-score" attachedToRef="score" cancelActivity="false"><compensateEventDefinition/></boundaryEvent>
            <sequenceFlow id="to-undo" sourceRef="compensate-score" targetRef="undo"/>
            <userTask id="undo" name="Undo score" isForCompensation="true"/>
            <sequenceFlow id="undo-to-end" sourceRef="undo" targetRef="compensated"/><endEvent id="compensated"/>
            <sequenceFlow id="to-throw" sourceRef="score" targetRef="throw-compensation"/>
            <intermediateThrowEvent id="throw-compensation"><compensateEventDefinition/></intermediateThrowEvent>
            <sequenceFlow id="to-end" sourceRef="throw-compensation" targetRef="end"/><endEvent id="end"/>
          </process>
        </definitions>
        """;

    private sealed record RuntimeInstance(Guid Id, ProcessInstanceStatus Status, IReadOnlyList<string> ActiveTokens, Dictionary<string, JsonElement> Variables);
    private sealed record PersistedTask(Guid Id, string Name, UserTaskStatus Status);
}
