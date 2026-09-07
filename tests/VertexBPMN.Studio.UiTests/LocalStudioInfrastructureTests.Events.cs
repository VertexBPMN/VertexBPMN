using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Matrix 2.1-2.4 events: timed / message / signal intermediate catch (proven resume patterns)
// plus message- and signal-start coverage which the API runtime does NOT auto-instantiate.
//
// Engine facts established while authoring:
//  * An intermediateCatchEvent parks the instance until its trigger fires; the API host runs a
//    timer scheduler (~5s poll) so a PT2S timer catch fires in-test.
//  * Message/signal correlation is retried until the runtime reports it correlated (a
//    subscription-registration race exists right after the instance parks).
//  * Message- and signal-START events have no auto-instantiation path in the API runtime:
//    correlating/broadcasting without a target instance returns "not_found" and creates no
//    instance. These are covered as documented limitations (Assert.Skip), not forced red tests.

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Events_MessageIntermediateCatch_PausesThenResumesOnCorrelate()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_MsgCatch_{host.RunId}";
        var messageName = "go-" + Guid.NewGuid().ToString("N")[..6];

        await DeployUnderTestAsync(apiClient, processKey, BuildCatchBpmn(processKey, messageName, signalName: null));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"mc-{host.RunId}");

        // The process parks at the intermediate message catch (not completed yet).
        Assert.NotEqual("Completed", await GetInstanceStateAsync(instanceId));

        // Correlate (retrying over the subscription race) and the instance resumes to completion.
        await CorrelateMessageUntilCorrelatedAsync(apiClient, messageName, instanceId);
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "end") && IsEndEventReached(e));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Events_SignalIntermediateCatch_PausesThenResumesOnBroadcast()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_SigCatch_{host.RunId}";
        var signalName = "release-" + Guid.NewGuid().ToString("N")[..6];

        await DeployUnderTestAsync(apiClient, processKey, BuildCatchBpmn(processKey, messageName: null, signalName));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"sc-{host.RunId}");

        Assert.NotEqual("Completed", await GetInstanceStateAsync(instanceId));

        await BroadcastSignalUntilAsync(apiClient, signalName, instanceId);
        Assert.Equal("Completed", await GetInstanceStateAsync(instanceId));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Events_TimerIntermediateCatch_FiresAfterDuration()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_TmrCatch_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildTimerCatchBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"tc-{host.RunId}");

        // Parked at the timer catch until the PT2S duration elapses and the scheduler fires it.
        await WaitForInstanceStateAsync(instanceId, "Completed", TimeSpan.FromSeconds(35));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Events_MessageStartEvent_CorrelatingAutoInstantiates()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_MsgStart_{host.RunId}";
        var messageName = $"kickoff-{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildStartBpmn(processKey, messageName: messageName, signalName: null));
        host.RegisterProcessDefinitionCleanup(processKey);

        // Correlate WITHOUT a target instance -> the message-start process auto-instantiates.
        var result = await CorrelateMessageAsync(apiClient, messageName);
        Assert.Equal("correlated", result.GetProperty("resultType").GetString());

        var instanceId = Guid.Parse(result.GetProperty("processInstanceId").GetString()!);
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "end") && IsEndEventReached(e));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Events_SignalStartEvent_BroadcastAutoInstantiates()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_SigStart_{host.RunId}";
        var signalName = $"launch-{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildStartBpmn(processKey, messageName: null, signalName));
        host.RegisterProcessDefinitionCleanup(processKey);

        var definitionId = await GetProcessDefinitionIdByKeyAsync(apiClient, processKey);

        await BroadcastSignalAsync(apiClient, signalName);

        // Broadcast returns void, so locate the newly auto-started instance via the definition filter.
        var instanceId = await WaitForProcessInstanceByDefinitionAsync(apiClient, definitionId);
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "end") && IsEndEventReached(e));
    }

    // ---- helpers ----

    private static bool IsEventType(JsonElement e, string eventType)
        => e.TryGetProperty("eventType", out var et) && et.GetString() == eventType;

    private static bool IsEndEventReached(JsonElement e)
        => e.TryGetProperty("eventType", out var et) && et.GetString() == "END_EVENT_REACHED";

    private async Task CorrelateMessageUntilCorrelatedAsync(HttpClient apiClient, string messageName, Guid instanceId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var result = await CorrelateMessageAsync(apiClient, messageName, instanceId);
            if (result.TryGetProperty("resultType", out var rt)
                && string.Equals(rt.GetString(), "correlated", StringComparison.OrdinalIgnoreCase))
                return;
            await Task.Delay(300, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"Message catch '{messageName}' on instance {instanceId} never correlated.");
    }

    /// <summary>Resolves a deployed process definition's id by key (GET api/vertex/process-definition?key=).</summary>
    private static async Task<Guid> GetProcessDefinitionIdByKeyAsync(HttpClient apiClient, string processKey)
    {
        using var response = await apiClient.GetAsync(
            $"api/vertex/process-definition?key={Uri.EscapeDataString(processKey)}",
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.EnumerateArray().First();
        return Guid.Parse(first.GetProperty("id").GetString()!);
    }

    /// <summary>Polls GET api/vertex/process-instance?processDefinitionId= until the auto-started instance appears.</summary>
    private static async Task<Guid> WaitForProcessInstanceByDefinitionAsync(HttpClient apiClient, Guid definitionId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            using var response = await apiClient.GetAsync(
                $"api/vertex/process-instance?processDefinitionId={definitionId}",
                TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(response.IsSuccessStatusCode, body);
            using var doc = JsonDocument.Parse(body);
            var instances = doc.RootElement.EnumerateArray().ToArray();
            if (instances.Length > 0)
                return Guid.Parse(instances[^1].GetProperty("id").GetString()!);
            await Task.Delay(300, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"No auto-started instance for process definition {definitionId} appeared after broadcast.");
    }

    // Builds: Start -> intermediate catch (message OR signal) -> end.
    private static string BuildCatchBpmn(string processKey, string? messageName, string? signalName)
    {
        string definition =
            !string.IsNullOrWhiteSpace(messageName)
                ? $"""<message id="ck-msg" name="{messageName}" />"""
                : $"""<signal id="ck-sig" name="{signalName}" />""";
        string eventDef =
            !string.IsNullOrWhiteSpace(messageName)
                ? """<messageEventDefinition messageRef="ck-msg" />"""
                : """<signalEventDefinition signalRef="ck-sig" />""";
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              {{definition}}
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-catch" sourceRef="start" targetRef="catch" />
                <intermediateCatchEvent id="catch">
                  {{eventDef}}
                </intermediateCatchEvent>
                <sequenceFlow id="catch-to-end" sourceRef="catch" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }

    // Builds a process whose ONLY top-level start event is a message OR signal start, auto-instantiated
    // by correlating/broadcasting the matching event. start -> end (completes immediately).
    private static string BuildStartBpmn(string processKey, string? messageName, string? signalName)
    {
        string definition =
            !string.IsNullOrWhiteSpace(messageName)
                ? $"""<message id="st-msg" name="{messageName}" />"""
                : $"""<signal id="st-sig" name="{signalName}" />""";
        string startDef =
            !string.IsNullOrWhiteSpace(messageName)
                ? """<messageEventDefinition messageRef="st-msg" />"""
                : """<signalEventDefinition signalRef="st-sig" />""";
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              {{definition}}
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start">
                  {{startDef}}
                </startEvent>
                <sequenceFlow id="start-to-end" sourceRef="start" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }

    private static string BuildTimerCatchBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-catch" sourceRef="start" targetRef="catch" />
                <intermediateCatchEvent id="catch">
                  <timerEventDefinition>
                    <timeDuration><![CDATA[PT2S]]></timeDuration>
                  </timerEventDefinition>
                </intermediateCatchEvent>
                <sequenceFlow id="catch-to-end" sourceRef="catch" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }
}
