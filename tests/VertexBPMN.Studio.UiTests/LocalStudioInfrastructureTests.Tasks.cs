using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Matrix 2.9 Task types (execution semantics via the API).
//
// Coverage mapping (against the existing LocalStudioInfrastructureTests, confirmed by reading them):
//  * userTask (assignee, form, claim, complete, due-date UI)  -> already covered by
//    BpmnRuntime_StartsClaimsCompletesAndShowsPersistedHistory_WithARealTaskForm and
//    ExecutionDetails_LoadsJobsIncidentsAndVariables_ForARealInstance.
//  * failing serviceTask -> incident surfaced in UI            -> ExecutionDetails_..._ForARealInstance.
// New here: a *successful* service task via a real registered handler (calculateScore) - previously
// only the failing path was exercised.
//
// Engine facts established while authoring:
//  * serviceTask resolves node.Implementation against the service-task registry; a pure handler
//    (CalculateScoreServiceTaskHandler) computes creditScore from applicantName/age and succeeds,
//    emitting SERVICE_TASK_COMPLETED.
//  * scriptTask is intentionally DISABLED in the production runtime ("In-process script task
//    execution is disabled") - documented limitation, not a bug in the test.

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Tasks_ServiceTask_ExecutesRegisteredHandlerAndCompletes()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_SvcT_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildServiceTaskBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessWithVariablesAsync(
            apiClient, processKey, tenantId: null, businessKey: $"svc-{host.RunId}",
            new Dictionary<string, object> { ["applicantName"] = "Ada", ["age"] = 42 });

        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "score") && IsEventType(e, "SERVICE_TASK_COMPLETED"));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Tasks_ScriptTask_ExecutesAndCompletes()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_ScriptT_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildScriptTaskBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessWithVariablesAsync(
            apiClient, processKey, tenantId: null, businessKey: $"script-{host.RunId}",
            new Dictionary<string, object> { ["amount"] = 21 });

        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "scr") && IsEventType(e, "SCRIPT_TASK_COMPLETED"));
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task Tasks_ScriptTask_DefaultsToJavaScript()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_ScriptJS_{host.RunId}";

        // No <scriptFormat> -> the runtime defaults to JavaScript (Jint), not C#.
        await DeployUnderTestAsync(apiClient, processKey, BuildJavaScriptScriptTaskBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"scriptjs-{host.RunId}");

        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "scr") && IsEventType(e, "SCRIPT_TASK_COMPLETED"));
    }

    // ---- helpers ----

    private static string BuildJavaScriptScriptTaskBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-scr" sourceRef="start" targetRef="scr" />
                <scriptTask id="scr" name="Apply discount">
                  <script><![CDATA[40 + 2]]></script>
                  <resultVariable>scriptResult</resultVariable>
                </scriptTask>
                <sequenceFlow id="scr-to-end" sourceRef="scr" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }

    private static string BuildScriptTaskBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-scr" sourceRef="start" targetRef="scr" />
                <scriptTask id="scr" name="Apply discount">
                  <scriptFormat>C#</scriptFormat>
                  <script><![CDATA[variables["doubled"] = int.Parse(variables["amount"].ToString()!) * 2;]]></script>
                  <resultVariable>doubled</resultVariable>
                </scriptTask>
                <sequenceFlow id="scr-to-end" sourceRef="scr" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }

    private static string BuildServiceTaskBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-svc" sourceRef="start" targetRef="score" />
                <serviceTask id="score" name="Calculate credit score" implementation="calculateScore" />
                <sequenceFlow id="svc-to-end" sourceRef="score" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }
}
