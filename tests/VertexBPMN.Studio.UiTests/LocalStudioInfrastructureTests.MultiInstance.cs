using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Matrix 2.7 Multi-Instance (parallel vs sequential user task over a fixed loopCardinality).
//
// Engine facts established while authoring:
//  * The parser reads multiInstanceLoopCharacteristics from both subProcess and task elements
//    (BpmnParser), and the runtime expands a fixed <loopCardinality> into N task instances sharing
//    one MultiInstanceExecutionId (PersistentProcessExecutionRuntime.CompleteMultiInstanceIterationAsync).
//  * Parallel ("isSequential=false") puts all N instances open at once; sequential creates one at a
//    time. Each instance is an ordinary user task surfaced through api/task with the same activityId.
//  * Completing every instance advances the loop; after the last one, the activity completes.

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task MultiInstance_ParallelUserTask_OpensAllInstancesAndCompletesAfterAllDone()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_MIPar_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildMultiInstanceBpmn(processKey, isSequential: false, count: 3));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"mi-par-{host.RunId}");

        // Parallel: all three review tasks are open at the same time.
        await WaitForOpenTaskCountAsync(instanceId, 3);

        var open = await GetOpenTasksAsync(instanceId);
        Assert.Equal(3, open.Length);
        foreach (var task in open)
            Assert.Equal("mi-review", task.GetProperty("activityId").GetString());

        // The loop must not complete until every instance is done.
        var historyBefore = await GetHistoryAsync(instanceId);
        Assert.DoesNotContain(historyBefore, e => EventHasElementId(e, "end") && IsEndEventReached(e));

        foreach (var task in open)
            await CompleteTaskAsync(task.GetProperty("id").GetGuid());

        await WaitForInstanceStateAsync(instanceId, "Completed");

        // Exactly three iterations ran against the same multi-instance activity.
        var history = await GetHistoryAsync(instanceId);
        var created = history.Count(e => EventHasElementId(e, "mi-review") && IsEventType(e, "USER_TASK_CREATED"));
        Assert.Equal(3, created);
    }

    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task MultiInstance_SequentialUserTask_RunsOneInstanceAtATime()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_MISeq_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildMultiInstanceBpmn(processKey, isSequential: true, count: 3));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"mi-seq-{host.RunId}");

        // Sequential: exactly one instance is open at a time; after the last one the loop completes.
        for (var iteration = 1; iteration <= 3; iteration++)
        {
            await WaitForOpenTaskCountAsync(instanceId, 1);
            var open = await GetOpenTasksAsync(instanceId);
            Assert.Equal(1, open.Length);
            Assert.Equal("mi-review", open[0].GetProperty("activityId").GetString());
            await CompleteTaskAsync(open[0].GetProperty("id").GetGuid());
        }

        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        var created = history.Count(e => EventHasElementId(e, "mi-review") && IsEventType(e, "USER_TASK_CREATED"));
        Assert.Equal(3, created);
    }

    // ---- helpers ----

    private static string BuildMultiInstanceBpmn(string processKey, bool isSequential, int count)
    {
        var seq = isSequential ? "true" : "false";
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <startEvent id="start" />
                <sequenceFlow id="to-mi" sourceRef="start" targetRef="mi-review" />
                <userTask id="mi-review" name="Review line item">
                  <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                  <multiInstanceLoopCharacteristics isSequential="{{seq}}">
                    <loopCardinality>{{count}}</loopCardinality>
                  </multiInstanceLoopCharacteristics>
                </userTask>
                <sequenceFlow id="mi-to-end" sourceRef="mi-review" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }
}
