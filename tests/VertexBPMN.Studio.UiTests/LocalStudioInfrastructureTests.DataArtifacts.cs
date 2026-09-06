using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Matrix 2.10 Data artifacts (dataObject / dataObjectReference / dataStore / dataStoreReference /
// property). These are declarative elements: they carry no execution token, so a process that
// declares them must still deploy and run to completion unchanged. This proves the parser and
// runtime tolerate data artifacts in a real deployed definition.

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task DataArtifacts_DataObjectsStoresAndProperties_DoNotBlockExecution()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        using var apiClient = host.CreateApiClient();
        var processKey = $"StudioE2E_DataArt_{host.RunId}";

        await DeployUnderTestAsync(apiClient, processKey, BuildDataArtifactsBpmn(processKey));
        host.RegisterProcessDefinitionCleanup(processKey);

        var instanceId = await StartProcessAsync(apiClient, processKey, null, $"da-{host.RunId}");

        await WaitForOpenTaskCountAsync(instanceId, 1);
        var open = await GetOpenTasksAsync(instanceId);
        Assert.Equal("t", open[0].GetProperty("activityId").GetString());

        await CompleteTaskAsync(open[0].GetProperty("id").GetGuid());
        await WaitForInstanceStateAsync(instanceId, "Completed");

        var history = await GetHistoryAsync(instanceId);
        Assert.Contains(history, e => EventHasElementId(e, "end") && IsEndEventReached(e));
    }

    // ---- helpers ----

    private static string BuildDataArtifactsBpmn(string processKey)
    {
        return $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                         xmlns:vertex="http://vertexbpmn.dev/schema"
                         targetNamespace="urn:vertex:test">
              <process id="{{processKey}}" isExecutable="true">
                <dataObject id="invoiceDoc" name="Invoice" />
                <dataObjectReference id="invoiceRef" dataObjectRef="invoiceDoc" />
                <dataStore id="archiveStore" name="Archive" />
                <dataStoreReference id="archiveRef" dataStoreRef="archiveStore" />
                <property id="invoiceAmount" name="amount" />
                <startEvent id="start" />
                <sequenceFlow id="to-t" sourceRef="start" targetRef="t" />
                <userTask id="t" name="Process invoice">
                  <extensionElements><vertex:assignee>yova</vertex:assignee></extensionElements>
                </userTask>
                <sequenceFlow id="t-to-end" sourceRef="t" targetRef="end" />
                <endEvent id="end" />
              </process>
            </definitions>
            """;
    }
}
