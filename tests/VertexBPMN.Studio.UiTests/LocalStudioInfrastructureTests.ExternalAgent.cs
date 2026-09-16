using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    public async Task ExternalAgentPreset_DeploysAndShowsProcessScopedWaitingJob()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");
        var processKey = $"AgentReview_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(processKey);
        var page = await host.CreatePageAsync();
        try
        {
            await OpenBpmnModelerAsync(page);
            await SelectTenantAsync(page, host.AgentTenantName, host.TenantId);
            await ImportBpmnAsync(page, EmptyProcess(processKey));
            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
            await page.GetByTestId("low-code-node-catalog").GetByRole(AriaRole.Button,
                new() { Name = "Agent contract review → human review", Exact = true }).ClickAsync();

            var xml = await WaitForPreviewXmlAsync(page, "vertex:externalTask");
            Assert.Contains("agentProfileRef=\"contract-reviewer.v1\"", xml, StringComparison.Ordinal);
            Assert.Contains("name=\"result\" target=\"contractReview\"", xml, StringComparison.Ordinal);
            Assert.Contains("candidateGroups=\"contract-reviewers\"", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", xml, StringComparison.OrdinalIgnoreCase);
            await page.GetByRole(AriaRole.Button, new() { Name = "Validate", Exact = true }).ClickAsync();
            await page.GetByText("No issues", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
            await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();

            using var api = host.CreateApiClient();
            using var started = await api.PostAsJsonAsync("api/runtime/start", new
            {
                processDefinitionKey = processKey,
                tenantId = host.TenantId,
                businessKey = $"agent-review-{host.RunId}",
                variables = new
                {
                    document = "Section one\nThis agreement renews automatically.",
                    documentId = $"contract-{host.RunId}",
                    documentVersion = "v1"
                }
            }, TestContext.Current.CancellationToken);
            var body = await started.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(started.IsSuccessStatusCode, body);
            using var instance = JsonDocument.Parse(body);
            var processInstanceId = instance.RootElement.GetProperty("id").GetGuid();

            await page.GetByRole(AriaRole.Link, new() { Name = "Execution Details", Exact = true }).ClickAsync();
            await page.WaitForURLAsync("**/execution-details");
            var processInstanceInput = page.GetByLabel("Process instance id for variables", new() { Exact = true });
            await processInstanceInput.FillAsync(processInstanceId.ToString());
            await processInstanceInput.PressAsync("Tab");
            await page.GetByRole(AriaRole.Button, new() { Name = "Load agent jobs", Exact = true }).ClickAsync();
            var result = page.GetByTestId("external-task-operations-result");
            await result.GetByText("Ready — waiting for worker", new() { Exact = true }).WaitForAsync();
            await result.GetByText("agent.contract-review", new() { Exact = false }).WaitForAsync();
            Assert.DoesNotContain("document", await result.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    private static string EmptyProcess(string processKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
          xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" id="Defs_{{processKey}}" targetNamespace="urn:vertex:e2e">
          <bpmn:process id="{{processKey}}" isExecutable="true" />
          <bpmndi:BPMNDiagram id="Diagram_{{processKey}}"><bpmndi:BPMNPlane id="Plane_{{processKey}}" bpmnElement="{{processKey}}" /></bpmndi:BPMNDiagram>
        </bpmn:definitions>
        """;
}
