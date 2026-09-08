using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    public async Task EditorCorrections_RepositoryRejectsMalformedXml()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local Studio E2E only.");
        using var client = host.CreateApiClient();
        using var response = await client.PostAsJsonAsync("api/repository", new { bpmnXml = "<definitions", name = "malformed.bpmn" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EditorCorrections_CatalogApprovalWaitsAndCompletesThroughTheInbox()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local Studio E2E only.");
        var key = $"EditorApproval_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(key);
        using var client = host.CreateApiClient();
        var page = await host.CreatePageAsync();
        try
        {
            await OpenBpmnModelerAsync(page);
            await ImportBpmnAsync(page, CreateBpmn(key));
            await InsertCatalogNodeIntoFirstFlowAsync(page, "User approval form");
            await OpenBpmnToolTabAsync(page, "Test runs");
            await page.GetByTestId("bpmn-engine-test-run").GetByRole(AriaRole.Button, new() { Name = "Deploy and run test", Exact = true }).ClickAsync();
            await page.GetByText("Engine test run verified: a persistent wait state.", new() { Exact = true }).WaitForAsync();
            var instances = await client.GetFromJsonAsync<JsonElement[]>("api/runtime", TestContext.Current.CancellationToken);
            var instance = Assert.Single(instances!, i => i.GetProperty("processId").GetString() == key);
            var id = instance.GetProperty("id").GetGuid();
            var task = Assert.Single(await GetOpenTasksAsync(id));
            var taskId = task.GetProperty("id").GetGuid();
            await page.GotoAsync($"{host.StudioBaseAddress}tasks");
            await FillBoundInputAsync(page.GetByPlaceholder("Search"), id.ToString());
            var row = page.Locator("tr").Filter(new() { HasText = taskId.ToString() }).First;
            await row.GetByRole(AriaRole.Button, new() { Name = "Claim Task", Exact = true }).ClickAsync();
            await page.GetByText($"Task {taskId} claimed!", new() { Exact = true }).WaitForAsync();
            await row.GetByRole(AriaRole.Button, new() { Name = "Complete Task", Exact = true }).ClickAsync();
            await page.GetByText($"Task {taskId} completed!", new() { Exact = true }).WaitForAsync();
            await WaitForInstanceStateAsync(id, "Completed");
            Assert.Empty(await GetOpenTasksAsync(id));
        }
        finally { await host.ClosePageAsync(page); }
    }

    [Theory]
    [InlineData("userTask")]
    [InlineData("exclusiveGateway")]
    public async Task EditorCorrections_RepositoryRejectsOrphansWithoutPersisting(string type)
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local Studio E2E only.");
        using var client = host.CreateApiClient();
        var key = $"EditorInvalid_{type}_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(key);
        var document = XDocument.Parse(CreateBpmn(key));
        var process = document.Descendants().Single(e => e.Name.LocalName == "process");
        process.Add(new XElement(process.Name.Namespace + type, new XAttribute("id", "orphan")));
        using var response = await client.PostAsJsonAsync("api/repository", new { bpmnXml = document.ToString(), name = key + ".bpmn" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("DEP-UNREACHABLE-NODE", body);
        Assert.Contains("orphan", body);
        var definitions = await client.GetFromJsonAsync<JsonElement[]>($"api/repository?key={key}", TestContext.Current.CancellationToken);
        Assert.Empty(definitions!);
    }

    [Theory]
    [InlineData(5000)]
    [InlineData(10)]
    public async Task EditorCorrections_CatalogIfRoutesTheExportedDiagram(int amount)
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local Studio E2E only.");
        var key = $"EditorIf_{amount}_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(key);
        using var client = host.CreateApiClient();
        var page = await host.CreatePageAsync();
        try
        {
            await OpenBpmnModelerAsync(page);
            await ImportBpmnAsync(page, CreateBpmn(key));
            await InsertCatalogNodeIntoFirstFlowAsync(page, "IF condition");
            var xml = await WaitForPreviewXmlAsync(page, "exclusiveGateway");
            var document = XDocument.Parse(xml);
            var split = document.Descendants().Single(e => e.Name.LocalName == "exclusiveGateway" && e.Attribute("default") is not null);
            var conditional = document.Descendants().Single(e => e.Name.LocalName == "sequenceFlow" && e.Elements().Any(c => c.Name.LocalName == "conditionExpression"));
            conditional.Elements().Single(e => e.Name.LocalName == "conditionExpression").Value = "${amount > 1000}";
            // Edit the XML exported by the actual catalog operation, preserving all generated nodes and DI.
            await ImportBpmnAsync(page, document.ToString());
            await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
            await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();
            var id = await StartProcessWithVariablesAsync(client, key, null, key, new Dictionary<string, object> { ["amount"] = amount });
            await WaitForInstanceStateAsync(id, "Completed");
            var expected = amount > 1000 ? (string)conditional.Attribute("id")! : (string)split.Attribute("default")!;
            var history = await GetHistoryAsync(id);
            var splitEvent = Assert.Single(history, e => EventHasElementId(e, (string)split.Attribute("id")!)
                && e.GetProperty("eventType").GetString() == "EXCLUSIVE_GATEWAY_SELECTED");
            Assert.Equal(expected, GetGatewaySelectedFlowId([splitEvent], "EXCLUSIVE_GATEWAY_SELECTED"));
            var merge = document.Descendants().Single(e => e.Name.LocalName == "exclusiveGateway" && e.Attribute("default") is null);
            Assert.Contains(history, e => EventHasElementId(e, (string)merge.Attribute("id")!));
        }
        finally { await host.ClosePageAsync(page); }
    }

    private static async Task InsertCatalogNodeIntoFirstFlowAsync(IPage page, string name)
    {
        await page.GetByTestId("bpmn-modeler-shell").Locator(".djs-connection .djs-hit").First.ClickAsync(new() { Force = true });
        await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
        var catalog = page.GetByTestId("low-code-node-catalog");
        await catalog.GetByLabel("Search nodes").FillAsync(name);
        await catalog.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).ClickAsync();
    }
}
