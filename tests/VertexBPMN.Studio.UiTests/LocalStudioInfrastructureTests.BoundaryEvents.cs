using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Matrix 2.5 Boundary Events (Time/Messages/Signals), interrupting and non-interrupting.
// Use Case 6 (Timer escalation path).
//
// Engine facts established while authoring:
//  * A generic <task> emits NO history event (only END_EVENT_REACHED / *_GATEWAY_SELECTED /
//    USER_TASK_* / TIMER_FIRED etc. are recorded). So "the recovery path ran" is proven by the
//    instance reaching Completed (interrupting) or by TIMER_FIRED history (timer), never by
//    asserting a generic task elementId.
//  * The API host runs a timer scheduler (~5s poll), so PT2S timer boundaries fire in-test.
//  * Message/signal correlation has a subscription-registration race right after start, so the
//    trigger is retried until the runtime reports it "correlated" / the instance progresses.

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task BoundaryEvents_TimerBoundaryInterrupting_EscalatesAfterTimeout()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_TmrBnd_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildRefundBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"uc6-{host.RunId}");

        // UC 6: leave the "wait for approval" task open; the PT2S timer must interrupt it.
        await WaitForInstanceStateAsync(instanceId, "Completed", TimeSpan.FromSeconds(35));

        var history = await GetHistoryAsync(instanceId);
        // The interrupting timer took the escalation path (TIMER_FIRED on the boundary element) ...
        Assert.Contains(history, e => EventHasElementId(e, "boundary"));
        // ... and the original approval task was interrupted, NOT completed by a user action.
        Assert.DoesNotContain(history, e => EventHasElementId(e, "wait") && IsUserTaskCompleted(e));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task BoundaryEvents_MessageBoundaryInterrupting_CancelsTaskAndContinues()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_MsgBndI_{host.RunId}";
        var messageName = $"refund-{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildMessageBoundaryBpmn(processKey, messageName, interrupting: true));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"mbndi-{host.RunId}");

        await CorrelateMessageUntilCompletedAsync(apiClient, messageName, instanceId);

        var history = await GetHistoryAsync(instanceId);
        // Message boundary fired (MESSAGE_CORRELATED on the boundary element) ...
        Assert.Contains(history, e => EventHasElementId(e, "boundary") && IsCorrelated(e, "MESSAGE"));
        // ... and the attached approval task was interrupted, NOT completed by a user action.
        Assert.DoesNotContain(history, e => EventHasElementId(e, "wait") && IsUserTaskCompleted(e));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task BoundaryEvents_MessageBoundaryNonInterrupting_KeepsTaskAndEscalatesAlongside()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_MsgBndN_{host.RunId}";
        var messageName = $"note-{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildMessageBoundaryBpmn(processKey, messageName, interrupting: false));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"mbndn-{host.RunId}");

        await CorrelateMessageUntilCorrelatedAsync(apiClient, messageName, instanceId);

        // Original task is STILL open (non-interrupting runs recovery alongside it).
        await WaitForOpenTaskCountAsync(instanceId, 1);
        Assert.NotEqual("Completed", await GetInstanceStateAsync(instanceId));

        var open = await GetOpenTasksAsync(instanceId);
        await CompleteTaskAsync(open[0].GetProperty("id").GetGuid());
        await WaitForInstanceStateAsync(instanceId, "Completed");
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task BoundaryEvents_SignalBoundaryInterrupting_CancelsTaskAndContinues()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_SigBndI_{host.RunId}";
        var signalName = $"cancel-{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildSignalBoundaryBpmn(processKey, signalName, interrupting: true));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"sbndi-{host.RunId}");

        await BroadcastSignalUntilAsync(apiClient, signalName, instanceId);

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "boundary") && IsCorrelated(e, "SIGNAL"));
        Assert.DoesNotContain(history, e => EventHasElementId(e, "wait") && IsUserTaskCompleted(e));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task BoundaryEvents_TimerBoundaryNonInterrupting_KeepsTaskAndEscalatesAlongside()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_TmrBndN_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildNonInterruptingTimerBoundaryBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"tbndn-{host.RunId}");

        // The non-interrupting timer fires on its own; wait until it is recorded in history.
        await WaitForHistoryContainsElementAsync(instanceId, "boundary", TimeSpan.FromSeconds(35));

        // Original task is STILL open (non-interrupting runs recovery alongside it).
        await WaitForOpenTaskCountAsync(instanceId, 1);
        Assert.NotEqual("Completed", await GetInstanceStateAsync(instanceId));

        var open = await GetOpenTasksAsync(instanceId);
        await CompleteTaskAsync(open[0].GetProperty("id").GetGuid());
        await WaitForInstanceStateAsync(instanceId, "Completed");
    }

    // ---- helpers ----

    private static bool IsUserTaskCompleted(JsonElement e)
        => e.TryGetProperty("eventType", out var et) && et.GetString() == "USER_TASK_COMPLETED";

    private static bool IsCorrelated(JsonElement e, string kind)
        => e.TryGetProperty("eventType", out var et)
           && string.Equals(et.GetString(), $"{kind}_CORRELATED", StringComparison.OrdinalIgnoreCase);

    private async Task WaitForOpenTaskCountAsync(Guid instanceId, int expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var tasks = await GetOpenTasksAsync(instanceId);
            if (tasks.Length == expected)
                return;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"Instance {instanceId} did not reach open-task count {expected}.");
    }

    private async Task BroadcastSignalUntilAsync(HttpClient apiClient, string signalName, Guid instanceId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (string.Equals(await GetInstanceStateAsync(instanceId), "Completed", StringComparison.OrdinalIgnoreCase))
                return;
            await BroadcastSignalAsync(apiClient, signalName);
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"Signal boundary '{signalName}' never completed instance {instanceId}.");
    }

    private async Task CorrelateMessageUntilCompletedAsync(HttpClient apiClient, string messageName, Guid instanceId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (string.Equals(await GetInstanceStateAsync(instanceId), "Completed", StringComparison.OrdinalIgnoreCase))
                return;
            await CorrelateMessageAsync(apiClient, messageName, instanceId);
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"Message boundary '{messageName}' never completed instance {instanceId}.");
    }

    // UC 6 / Matrix 2.5: interrupting timer boundary (fixed PT2S duration) on the approval task.
    private static string BuildRefundBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-wait" sourceRef="start" targetRef="wait" />
                <userTask id="wait" name="Wait for approval">
                  <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                </userTask>
                <boundaryEvent id="boundary" attachedToRef="wait" cancelActivity="true">
                  <timerEventDefinition>
                    <timeDuration><![CDATA[PT2S]]></timeDuration>
                  </timerEventDefinition>
                </boundaryEvent>
                <sequenceFlow id="b-to-rec" sourceRef="boundary" targetRef="recovery" />
                <task id="recovery" name="Recovery" />
                <sequenceFlow id="rec-to-end" sourceRef="recovery" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }

    private static string BuildNonInterruptingTimerBoundaryBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-wait" sourceRef="start" targetRef="wait" />
                <userTask id="wait" name="Wait for approval">
                  <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                </userTask>
                <boundaryEvent id="boundary" attachedToRef="wait" cancelActivity="false">
                  <timerEventDefinition>
                    <timeDuration><![CDATA[PT2S]]></timeDuration>
                  </timerEventDefinition>
                </boundaryEvent>
                <sequenceFlow id="b-to-rec" sourceRef="boundary" targetRef="recovery" />
                <task id="recovery" name="Recovery" />
                <sequenceFlow id="rec-to-end" sourceRef="recovery" targetRef="end" />
                <sequenceFlow id="wait-to-end" sourceRef="wait" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }

    private static string BuildMessageBoundaryBpmn(string processKey, string messageName, bool interrupting)
    {
        var cancelActivity = interrupting ? "true" : "false";
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <message id="msg1" name="{{messageName}}" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-wait" sourceRef="start" targetRef="wait" />
                <userTask id="wait" name="Wait for approval">
                  <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                </userTask>
                <boundaryEvent id="boundary" attachedToRef="wait" cancelActivity="{{cancelActivity}}">
                  <messageEventDefinition messageRef="msg1" />
                </boundaryEvent>
                <sequenceFlow id="b-to-rec" sourceRef="boundary" targetRef="recovery" />
                <task id="recovery" name="Recovery" />
                <sequenceFlow id="rec-to-end" sourceRef="recovery" targetRef="end" />
                <sequenceFlow id="wait-to-end" sourceRef="wait" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }

    private static string BuildSignalBoundaryBpmn(string processKey, string signalName, bool interrupting)
    {
        var cancelActivity = interrupting ? "true" : "false";
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <signal id="sig1" name="{{signalName}}" />
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-wait" sourceRef="start" targetRef="wait" />
                <userTask id="wait" name="Wait for approval">
                  <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                </userTask>
                <boundaryEvent id="boundary" attachedToRef="wait" cancelActivity="{{cancelActivity}}">
                  <signalEventDefinition signalRef="sig1" />
                </boundaryEvent>
                <sequenceFlow id="b-to-rec" sourceRef="boundary" targetRef="recovery" />
                <task id="recovery" name="Recovery" />
                <sequenceFlow id="rec-to-end" sourceRef="recovery" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }
}
