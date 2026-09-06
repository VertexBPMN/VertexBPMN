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
    public void Tasks_ScriptTask_CoverageLimitation()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        // PersistentProcessExecutionRuntime rejects scriptTask: "In-process script task execution is
        // disabled." Documented coverage limitation rather than forcing a red test (Matrix 2.9).
        Assert.Skip("scriptTask execution is disabled in the production runtime — documented coverage limitation (Matrix 2.9).");
    }

    // ---- helpers ----

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
