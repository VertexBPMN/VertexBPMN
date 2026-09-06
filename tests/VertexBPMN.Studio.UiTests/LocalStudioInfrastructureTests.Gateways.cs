using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Gateway control-flow semantics (matrix section 2.6 + plan UC 2 / UC 3).
// Strategy B: deploy hand-written BPMN via the real API, then drive execution via the API and
// verify against real history events (api/history/by-process-instance/{id}). The engine emits
// gateway selection events whose Data/FlowId identify the branch actually taken, and USER_TASK_*
// events carry the task elementId. Generic <task> nodes emit NO history event, so the branch
// verification uses userTask elementIds + gateway-selection events, not generic task ids.
public sealed partial class LocalStudioInfrastructureTests
{
    [Theory]
    [InlineData(5000, "path-a", "flow-a")]
    [InlineData(10, "path-b", "flow-default")]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Gateways_ExclusiveGateway_TakesConditionBranchAndSkipsTheOther(int amount, string expectedTaskId, string expectedFlowId)
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_XorGw_{host.RunId}_{amount}";
        host.RegisterProcessDefinitionCleanup(processKey);

        var bpmn = CreateExclusiveGatewayBpmn(processKey, "${amount > 1000}");
        await DeployUnderTestAsync(apiClient, processKey, bpmn);

        var instanceId = await StartProcessWithVariablesAsync(
            apiClient, processKey, tenantId: null, businessKey: $"xor-{amount}-{host.RunId}",
            new Dictionary<string, object> { ["amount"] = amount });

        // Only the chosen branch's user task may appear; complete it to reach the end.
        var open = await GetOpenTasksAsync(instanceId);
        var openTask = Assert.Single(open);
        Assert.Equal(expectedTaskId, openTask.GetProperty("activityId").GetString());
        await CompleteTaskAsync(openTask.GetProperty("id").GetGuid());

        await WaitForInstanceStateAsync(instanceId, "Completed");
        var history = await GetHistoryAsync(instanceId);

        // The authoritative signal for which branch was taken is the gateway selection's flow id.
        Assert.Equal(expectedFlowId, GetGatewaySelectedFlowId(history, "EXCLUSIVE_GATEWAY_SELECTED"));

        // The chosen task actually ran; the other path must never have been instantiated.
        Assert.Contains(history, e => EventHasElementId(e, expectedTaskId));
        var otherTaskId = expectedTaskId == "path-a" ? "path-b" : "path-a";
        Assert.DoesNotContain(history, e => EventHasElementId(e, otherTaskId));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Gateways_ParallelGateway_ForksBothBranchesAndJoinsOnlyAfterBothComplete()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_ParGw_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(processKey);

        var bpmn = CreateParallelGatewayBpmn(processKey);
        await DeployUnderTestAsync(apiClient, processKey, bpmn);

        var instanceId = await StartProcessWithVariablesAsync(
            apiClient, processKey, tenantId: null, businessKey: $"par-{host.RunId}",
            new Dictionary<string, object>());

        // Fork: BOTH parallel user tasks must be open simultaneously before either finishes.
        var open = Array.Empty<JsonElement>();
        var forkDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < forkDeadline && open.Length < 2)
        {
            open = await GetOpenTasksAsync(instanceId);
            if (open.Length < 2) await Task.Delay(250, TestContext.Current.CancellationToken);
        }
        Assert.Equal(2, open.Length);

        // Complete only one branch: the join must NOT release yet.
        await CompleteTaskAsync(open[0].GetProperty("id").GetGuid());
        var midState = await GetInstanceStateAsync(instanceId);
        Assert.NotEqual("Completed", midState);

        // After the second branch completes, the join releases and the process ends.
        await CompleteTaskAsync(open[1].GetProperty("id").GetGuid());
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "par-task-a"));
        Assert.Contains(history, e => EventHasElementId(e, "par-task-b"));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Gateways_InclusiveGateway_RunsExactlyTheTrueConditionsAndJoinsOnlyThose()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_IncGw_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(processKey);

        var bpmn = CreateInclusiveGatewayBpmn(processKey);
        await DeployUnderTestAsync(apiClient, processKey, bpmn);

        var instanceId = await StartProcessWithVariablesAsync(
            apiClient, processKey, tenantId: null, businessKey: $"inc-{host.RunId}",
            new Dictionary<string, object> { ["x"] = true, ["y"] = true });

        // Exactly the two true-condition branches become user tasks; the false branch never runs.
        var open = Array.Empty<JsonElement>();
        var forkDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < forkDeadline && open.Length < 2)
        {
            open = await GetOpenTasksAsync(instanceId);
            if (open.Length < 2) await Task.Delay(250, TestContext.Current.CancellationToken);
        }
        Assert.Equal(2, open.Length);
        foreach (var task in open)
            await CompleteTaskAsync(task.GetProperty("id").GetGuid());

        // If the inclusive join incorrectly waited for ALL three branches, this would deadlock/time-out.
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "inc-task-a"));
        Assert.Contains(history, e => EventHasElementId(e, "inc-task-b"));
        Assert.DoesNotContain(history, e => EventHasElementId(e, "inc-task-c"));

        // The gateway selection lists exactly the two taken flows.
        var selected = GetGatewaySelectedFlowIds(history, "INCLUSIVE_GATEWAY_SELECTED");
        Assert.Equal(2, selected.Count);
        Assert.Contains("flow-inc-a", selected);
        Assert.Contains("flow-inc-b", selected);
        Assert.DoesNotContain("flow-inc-c", selected);
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Gateways_EventBasedGateway_TakesOnlyTheFirstArrivingEventAndCancelsTheOther()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_EvtGw_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(processKey);
        var messageName = $"gateway-msg-{host.RunId}";

        var bpmn = CreateEventBasedGatewayBpmn(processKey, messageName);
        await DeployUnderTestAsync(apiClient, processKey, bpmn);

        var instanceId = await StartProcessWithVariablesAsync(
            apiClient, processKey, tenantId: null, businessKey: $"evt-{host.RunId}",
            new Dictionary<string, object>());

        // Correlate the message immediately: the message branch should win over the 10s timer.
        // Retry briefly, because the catch-event subscription may register a moment after start
        // returns. The correlate endpoint always returns 200; a successful delivery has
        // resultType "correlated", a no-subscription match is "not_found" (lowercase).
        var correlationSucceeded = false;
        var correlateDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < correlateDeadline && !correlationSucceeded)
        {
            using var correlate = await apiClient.PostAsJsonAsync(
                "api/vertex/message",
                new { messageName, processInstanceId = instanceId, variables = new Dictionary<string, object>(), tenantId = (string?)null },
                TestContext.Current.CancellationToken);
            var body = await correlate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(correlate.IsSuccessStatusCode, body);
            var resultType = JsonDocument.Parse(body).RootElement.GetProperty("resultType").GetString();
            correlationSucceeded = string.Equals(resultType, "correlated", StringComparison.OrdinalIgnoreCase);
            if (!correlationSucceeded)
                await Task.Delay(500, TestContext.Current.CancellationToken);
        }
        Assert.True(correlationSucceeded, "Message was never correlated; the gateway message subscription did not become active.");

        // The message branch's user task must appear and be completable to reach the end.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        JsonElement[]? open = null;
        while (DateTime.UtcNow < deadline)
        {
            open = await GetOpenTasksAsync(instanceId);
            if (open.Length == 1) break;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }
        var task = Assert.Single(open ?? []);
        Assert.Equal("evt-task-message", task.GetProperty("activityId").GetString());
        await CompleteTaskAsync(task.GetProperty("id").GetGuid());

        // The instance must complete long before the 10s timer would have fired.
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "evt-task-message"));
        Assert.DoesNotContain(history, e => EventHasElementId(e, "evt-task-timer"));
    }

    // ---- fixtures -------------------------------------------------------------------------------

    private static async Task DeployUnderTestAsync(HttpClient apiClient, string processKey, string bpmnXml)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "api/repository",
            new { bpmnXml, name = $"{processKey}.bpmn", tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static bool EventHasElementId(JsonElement historyEvent, string elementId) =>
        historyEvent.TryGetProperty("elementId", out var element)
        && element.GetString() == elementId;

    /// <summary>Reads the FlowId chosen by a gateway-selection history event (Data is a PascalCase JSON string).</summary>
    private static string? GetGatewaySelectedFlowId(JsonElement[] history, string eventType)
    {
        foreach (var historyEvent in history)
        {
            if (historyEvent.TryGetProperty("eventType", out var type) && type.GetString() == eventType
                && TryParseData(historyEvent, out var data)
                && data.TryGetProperty("FlowId", out var flowId))
            {
                return flowId.GetString();
            }
        }

        return null;
    }

    /// <summary>Reads the flowIds array chosen by an inclusive/complex gateway-selection event.</summary>
    private static List<string> GetGatewaySelectedFlowIds(JsonElement[] history, string eventType)
    {
        var result = new List<string>();
        foreach (var historyEvent in history)
        {
            if (historyEvent.TryGetProperty("eventType", out var type) && type.GetString() == eventType
                && TryParseData(historyEvent, out var data))
            {
                // Anonymous-type member name is preserved verbatim by AddHistory's default
                // JsonSerializer (no naming policy): the engine emits the lowercase "flowIds"
                // member for inclusive/complex selection. Accept both casings defensively.
                if (data.TryGetProperty("flowIds", out var flowIds)
                    && flowIds.ValueKind == JsonValueKind.Array)
                {
                    result.AddRange(flowIds.EnumerateArray().Select(item => item.GetString()!));
                }
                else if (data.TryGetProperty("FlowIds", out flowIds)
                         && flowIds.ValueKind == JsonValueKind.Array)
                {
                    result.AddRange(flowIds.EnumerateArray().Select(item => item.GetString()!));
                }
            }
        }

        return result;
    }

    private static bool TryParseData(JsonElement historyEvent, out JsonElement data)
    {
        data = default;
        if (!historyEvent.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.String)
            return false;
        try
        {
            using var doc = JsonDocument.Parse(dataElement.GetString()!);
            data = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CreateExclusiveGatewayBpmn(string processKey, string conditionBody) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/e2e">
          <process id="{{processKey}}" isExecutable="true">
            <startEvent id="start" />
            <sequenceFlow id="flow-to-gateway" sourceRef="start" targetRef="gateway" />
            <exclusiveGateway id="gateway" default="flow-default" />
            <sequenceFlow id="flow-a" sourceRef="gateway" targetRef="path-a">
              <conditionExpression><![CDATA[{{conditionBody}}]]></conditionExpression>
            </sequenceFlow>
            <sequenceFlow id="flow-default" sourceRef="gateway" targetRef="path-b" />
            <userTask id="path-a" name="Path A" />
            <userTask id="path-b" name="Path B" />
            <sequenceFlow id="flow-a-end" sourceRef="path-a" targetRef="end" />
            <sequenceFlow id="flow-b-end" sourceRef="path-b" targetRef="end" />
            <endEvent id="end" />
          </process>
        </definitions>
        """;

    private static string CreateParallelGatewayBpmn(string processKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/e2e">
          <process id="{{processKey}}" isExecutable="true">
            <startEvent id="start" />
            <sequenceFlow id="flow-to-fork" sourceRef="start" targetRef="fork" />
            <parallelGateway id="fork" />
            <sequenceFlow id="flow-to-a" sourceRef="fork" targetRef="par-task-a" />
            <sequenceFlow id="flow-to-b" sourceRef="fork" targetRef="par-task-b" />
            <userTask id="par-task-a" name="Parallel A" />
            <userTask id="par-task-b" name="Parallel B" />
            <sequenceFlow id="flow-a-join" sourceRef="par-task-a" targetRef="join" />
            <sequenceFlow id="flow-b-join" sourceRef="par-task-b" targetRef="join" />
            <parallelGateway id="join" />
            <sequenceFlow id="flow-join-end" sourceRef="join" targetRef="end" />
            <endEvent id="end" />
          </process>
        </definitions>
        """;

    private static string CreateInclusiveGatewayBpmn(string processKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/e2e">
          <process id="{{processKey}}" isExecutable="true">
            <startEvent id="start" />
            <sequenceFlow id="flow-to-fork" sourceRef="start" targetRef="fork" />
            <inclusiveGateway id="fork" />
            <sequenceFlow id="flow-inc-a" sourceRef="fork" targetRef="inc-task-a">
              <conditionExpression><![CDATA[${x == true}]]></conditionExpression>
            </sequenceFlow>
            <sequenceFlow id="flow-inc-b" sourceRef="fork" targetRef="inc-task-b">
              <conditionExpression><![CDATA[${y == true}]]></conditionExpression>
            </sequenceFlow>
            <sequenceFlow id="flow-inc-c" sourceRef="fork" targetRef="inc-task-c">
              <conditionExpression><![CDATA[${false}]]></conditionExpression>
            </sequenceFlow>
            <userTask id="inc-task-a" name="Inc A" />
            <userTask id="inc-task-b" name="Inc B" />
            <userTask id="inc-task-c" name="Inc C" />
            <sequenceFlow id="flow-a-join" sourceRef="inc-task-a" targetRef="join" />
            <sequenceFlow id="flow-b-join" sourceRef="inc-task-b" targetRef="join" />
            <sequenceFlow id="flow-c-join" sourceRef="inc-task-c" targetRef="join" />
            <inclusiveGateway id="join" />
            <sequenceFlow id="flow-join-end" sourceRef="join" targetRef="end" />
            <endEvent id="end" />
          </process>
        </definitions>
        """;

    private static string CreateEventBasedGatewayBpmn(string processKey, string messageName) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="https://vertexbpmn.dev/e2e">
          <message id="gateway-msg" name="{{messageName}}" />
          <process id="{{processKey}}" isExecutable="true">
            <startEvent id="start" />
            <sequenceFlow id="flow-to-gateway" sourceRef="start" targetRef="gateway" />
            <eventBasedGateway id="gateway" />
            <sequenceFlow id="flow-to-timer" sourceRef="gateway" targetRef="evt-timer-catch" />
            <sequenceFlow id="flow-to-message" sourceRef="gateway" targetRef="evt-message-catch" />
            <intermediateCatchEvent id="evt-timer-catch">
              <timerEventDefinition><timeDuration>PT10S</timeDuration></timerEventDefinition>
            </intermediateCatchEvent>
            <intermediateCatchEvent id="evt-message-catch">
              <messageEventDefinition messageRef="gateway-msg" />
            </intermediateCatchEvent>
            <sequenceFlow id="flow-timer-task" sourceRef="evt-timer-catch" targetRef="evt-task-timer" />
            <sequenceFlow id="flow-message-task" sourceRef="evt-message-catch" targetRef="evt-task-message" />
            <userTask id="evt-task-timer" name="Timer branch" />
            <userTask id="evt-task-message" name="Message branch" />
            <sequenceFlow id="flow-timer-end" sourceRef="evt-task-timer" targetRef="end" />
            <sequenceFlow id="flow-message-end" sourceRef="evt-task-message" targetRef="end" />
            <endEvent id="end" />
          </process>
        </definitions>
        """;
}
