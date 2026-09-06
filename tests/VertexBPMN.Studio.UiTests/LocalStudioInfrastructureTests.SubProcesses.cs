using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Matrix 2.8 Embedded sub-processes: an inner start -> user task -> inner end runs while the
// outer process waits on the sub-process; completing the inner task lets the sub-process and then
// the outer process finish.

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task SubProcesses_EmbeddedSubProcess_ExecutesContainedTaskThenCompletes()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_SubP_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildEmbeddedSubProcessBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"subp-{host.RunId}");

        // The contained user task is the only open task (the sub-process is active).
        await WaitForOpenTaskCountAsync(instanceId, 1);
        var open = await GetOpenTasksAsync(instanceId);
        Assert.Equal(1, open.Length);
        Assert.Equal("sp-task", open[0].GetProperty("activityId").GetString());

        // Completing the inner task completes the sub-process and then the whole instance.
        await CompleteTaskAsync(open[0].GetProperty("id").GetGuid());
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "sp-task") && IsEventType(e, "USER_TASK_COMPLETED"));
        Assert.Contains(history, e => EventHasElementId(e, "end") && IsEndEventReached(e));
    }

    // ---- helpers ----

    private static string BuildEmbeddedSubProcessBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-sp" sourceRef="start" targetRef="sp" />
                <subProcess id="sp" name="Approval routine">
                  <startEvent id="sp-start" />
                  <sequenceFlow id="sp-to-task" sourceRef="sp-start" targetRef="sp-task" />
                  <userTask id="sp-task" name="Approve">
                    <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                  </userTask>
                  <sequenceFlow id="sp-task-to-end" sourceRef="sp-task" targetRef="sp-end" />
                  <endEvent id="sp-end" />
                </subProcess>
                <sequenceFlow id="sp-to-end" sourceRef="sp" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }
}
