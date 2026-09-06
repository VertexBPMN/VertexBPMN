using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Matrix 2.11 Editor/bpmn.io artifacts embedded in a definition: lanes (laneSet/lane), text
// annotations and associations. Like data artifacts these are graphical/declarative elements with
// no execution token; a definition carrying them must still deploy and run to completion. This
// verifies the full round-trip a hand-built Modeler diagram (which emits BPMNDiagram, lanes,
// annotations) survives into the engine.

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task EditorFeatures_LanesAnnotationsAndAssociations_DoNotBlockExecution()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_EdFeat_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildEditorFeaturesBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"ef-{host.RunId}");

        await WaitForOpenTaskCountAsync(instanceId, 1);
        var open = await GetOpenTasksAsync(instanceId);
        Assert.Equal("t", open[0].GetProperty("activityId").GetString());

        await CompleteTaskAsync(open[0].GetProperty("id").GetGuid());
        await WaitForInstanceStateAsync(instanceId, "Completed");
    }

    // ---- helpers ----

    private static string BuildEditorFeaturesBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <laneSet id="ls1">
                  <lane id="lane1" name="Processing">
                    <flowNodeRef>t</flowNodeRef>
                  </lane>
                </laneSet>
                <startEvent id="start" />
                <sequenceFlow id="to-t" sourceRef="start" targetRef="t" />
                <userTask id="t" name="Handle request">
                  <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                </userTask>
                <sequenceFlow id="t-to-end" sourceRef="t" targetRef="end" />
                <endEvent id="end" />
                <textAnnotation id="note1">
                  <text>Manual follow-up required</text>
                </textAnnotation>
                <association id="a1" sourceRef="note1" targetRef="t" />
              </process>
              <BPMNDiagram id="diag1" xmlns="http://www.omg.org/spec/BPMN/20100524/DI">
                <BPMNPlane id="plane1" bpmnElement="{{processKey}}" />
              </BPMNDiagram>
            </definitions>
            """;
    }
}
