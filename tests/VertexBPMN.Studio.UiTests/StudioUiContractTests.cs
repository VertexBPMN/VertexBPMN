using System.Collections.Concurrent;
using System.Text;
using System.Xml.Linq;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed class StudioUiContractTests(StudioUiTestHost host) : IClassFixture<StudioUiTestHost>
{
    private static readonly string[] ExpectedNavigationRoutes =
    [
        "/", "/bpmn-modeler", "/dmn-modeler", "/cmmn-modeler", "/form-builder",
        "/process-definitions", "/tasks", "/process-instances", "/deployments", "/triggers",
        "/messages-signals", "/history", "/execution-details", "/event-log", "/analytics",
        "/performance", "/health", "/simulation", "/debugging", "/compliance",
        "/engine-management", "/tenants", "/credentials", "/configuration", "/feature-flags",
        "/migration", "/connectors", "/extensions", "/sso"
    ];

    private const string ImportableBpmn = """
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_Import" targetNamespace="https://vertexbpmn.io/ui-tests">
  <bpmn:process id="Process_Import" isExecutable="true">
    <bpmn:startEvent id="Start_Import" />
    <bpmn:sequenceFlow id="Flow_Import" sourceRef="Start_Import" targetRef="End_Import" />
    <bpmn:endEvent id="End_Import" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id="Diagram_Import">
    <bpmndi:BPMNPlane id="Plane_Import" bpmnElement="Process_Import">
      <bpmndi:BPMNShape id="Start_Import_di" bpmnElement="Start_Import"><dc:Bounds x="180" y="120" width="36" height="36" /></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="End_Import_di" bpmnElement="End_Import"><dc:Bounds x="380" y="120" width="36" height="36" /></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id="Flow_Import_di" bpmnElement="Flow_Import"><di:waypoint x="216" y="138" /><di:waypoint x="380" y="138" /></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>
""";

    [Fact]
    public async Task Dashboard_Renders_And_Loads_Runtime_Contracts()
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync(host.BaseAddress.ToString());

        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
        await page.GetByText("Approve invoice", new() { Exact = true }).WaitForAsync();

        Assert.Contains(host.ApiRequests, request => request.StartsWith("GET /api/runtime", StringComparison.Ordinal));
        Assert.Contains(host.ApiRequests, request => request.StartsWith("GET /api/task", StringComparison.Ordinal));
        Assert.Contains(host.ApiRequests, request => request.StartsWith("GET /api/repository", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessDefinitions_Renders_RepositoryContractData()
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync($"{host.BaseAddress}process-definitions");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync();
        await page.GetByText("InvoiceProcess", new() { Exact = true }).WaitForAsync();
        await page.GetByText("Invoice approval", new() { Exact = true }).WaitForAsync();
        await page.GetByText("1-1 of 1", new() { Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task Tasks_Renders_TaskContractData()
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync($"{host.BaseAddress}tasks");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Tasks", Exact = true }).WaitForAsync();
        await page.GetByText("Approve invoice", new() { Exact = true }).WaitForAsync();
    }

    [Theory]
    [InlineData("process-definitions", "Process Definitions", "Refresh")]
    [InlineData("process-instances", "Process Instances", "Refresh")]
    [InlineData("deployments", "Deployments", "Upload BPMN files")]
    [InlineData("history", "History", "Refresh")]
    [InlineData("execution-details", "Execution Details", "Load jobs")]
    public async Task OperationalPages_Preserve_PrimaryActions_Without_MobileOverflow(
        string route,
        string heading,
        string primaryAction)
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        await page.GotoAsync($"{host.BaseAddress}{route}");

        await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).First.WaitForAsync();
        await page.GetByText(primaryAction, new() { Exact = true }).First.WaitForAsync();

        var dimensions = await page.EvaluateAsync<int[]>(
            "() => [document.documentElement.clientWidth, document.documentElement.scrollWidth]");
        Assert.Equal(dimensions[0], dimensions[1]);
    }

    [Theory]
    [InlineData("triggers", "Workflow Triggers", "Select a tenant")]
    [InlineData("messages-signals", "Messages & Signals", "Message correlation")]
    [InlineData("event-log", "Event Log", "Refresh")]
    [InlineData("analytics", "Process Analytics", "Predictive analytics")]
    [InlineData("performance", "Performance", "Refresh")]
    [InlineData("health", "Health and Operations", "Refresh")]
    [InlineData("simulation", "Simulation", "Scenario configuration")]
    [InlineData("debugging", "Debugging Trace", "Trace BPMN XML")]
    [InlineData("compliance", "Compliance Evidence", "Available technical evidence")]
    [InlineData("engine-management", "Engine Management", "Connection management is external")]
    [InlineData("tenants", "Tenants", "Create tenant")]
    [InlineData("credentials", "Credentials", "Select a tenant")]
    [InlineData("configuration", "Configuration", "Engine capabilities")]
    [InlineData("feature-flags", "Feature Flags", "Refresh")]
    [InlineData("migration", "Process Migration", "Migration plan")]
    [InlineData("connectors", "Connectors", "Select a tenant")]
    [InlineData("extensions", "Extensions", "Installed plugins")]
    [InlineData("sso", "Single Sign-On (SSO)", "Azure Active Directory Integration")]
    [InlineData("counter", "Counter", "Increment")]
    [InlineData("Error", "Something went wrong", "Development mode")]
    public async Task RemainingPages_Use_ModernShell_And_DoNotOverflowOnMobile(
        string route,
        string heading,
        string preservedContent)
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        await page.GotoAsync($"{host.BaseAddress}{route}");

        await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).First.WaitForAsync();
        await page.GetByText(preservedContent, new() { Exact = true }).First.WaitForAsync();

        var dimensions = await page.EvaluateAsync<int[]>(
            "() => [document.documentElement.clientWidth, document.documentElement.scrollWidth]");
        Assert.Equal(dimensions[0], dimensions[1]);
    }

    [Fact]
    public async Task Shell_Groups_And_Preserves_All_Navigation_Routes()
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = 1440, Height = 900 } });
        await page.GotoAsync(host.BaseAddress.ToString());

        foreach (var section in new[] { "Overview", "Model", "Operate", "Analyze", "Manage" })
            await page.GetByText(section, new() { Exact = true }).WaitForAsync();

        var actualRoutes = await page.Locator("nav a.mud-nav-link").EvaluateAllAsync<string[]>(
            "links => links.map(link => new URL(link.href).pathname)");

        Assert.Equal(ExpectedNavigationRoutes.Order(), actualRoutes.Order());
        await page.GetByRole(AriaRole.Combobox, new() { Name = "Tenant", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Combobox, new() { Name = "Engine", Exact = true }).WaitForAsync();
        await page.GetByLabel("Refresh engines", new() { Exact = true }).WaitForAsync();
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(768, 1024)]
    public async Task Shell_Reflows_And_Exposes_Navigation_On_Narrow_Viewports(int width, int height)
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        await page.GotoAsync(host.BaseAddress.ToString());

        var dimensions = await page.EvaluateAsync<int[]>(
            "() => [document.documentElement.clientWidth, document.documentElement.scrollWidth]");
        Assert.Equal(dimensions[0], dimensions[1]);

        await page.Locator("[data-interactive-ready='true']").WaitForAsync();
        await page.GetByLabel("Toggle navigation", new() { Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "BPMN Modeler", Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task BpmnModeler_Mobile_Uses_A_Toggleable_Properties_Sheet_Without_PageOverflow()
    {
        var page = await host.Browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        await page.GotoAsync($"{host.BaseAddress}bpmn-modeler");
        await page.GetByTestId("bpmn-modeler-shell").WaitForAsync();

        var dimensions = await page.EvaluateAsync<int[]>(
            "() => [document.documentElement.clientWidth, document.documentElement.scrollWidth]");
        Assert.Equal(dimensions[0], dimensions[1]);

        var panel = page.GetByLabel("BPMN properties panel", new() { Exact = true });
        Assert.True(await panel.IsHiddenAsync());
        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Show properties", Exact = true });
        await toggle.ClickAsync();
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Assert.Equal("true", await page.GetByRole(AriaRole.Button, new() { Name = "Hide properties", Exact = true }).GetAttributeAsync("aria-expanded"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Hide properties", Exact = true }).ClickAsync();
        await panel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    [Theory]
    [InlineData("bpmn-modeler", "BPMN Modeler", "Deploy BPMN", "Export XML", "bpmn-modeler-shell", "Viewer", "bpmn-viewer-shell")]
    [InlineData("dmn-modeler", "DMN Modeler", "Deploy DMN", "Export DMN", "dmn-modeler-shell", "Viewer", "dmn-viewer-shell")]
    [InlineData("form-builder", "Form Builder", "Save form", "Export JSON", "form-builder-shell", "Runtime Viewer", "form-viewer-shell")]
    [InlineData("cmmn-modeler", "CMMN Modeler", "Register case model", "Export CMMN", "cmmn-modeler-shell", "Viewer", "cmmn-viewer-shell")]
    public async Task BpmnIoModelerShells_Render_EditorViewer_And_ActionButtons(
        string route,
        string heading,
        string primaryAction,
        string exportAction,
        string modelerTestId,
        string viewerTab,
        string viewerTestId)
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync($"{host.BaseAddress}{route}");

        await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = primaryAction, Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = exportAction, Exact = true }).WaitForAsync();
        await page.GetByTestId(modelerTestId).WaitForAsync();
        if (route == "bpmn-modeler")
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).WaitForAsync();
            await page.GetByTestId("bpmn-validation-sidebar").WaitForAsync();
            await OpenBpmnToolTabAsync(page, "XML");
            await page.GetByTestId("bpmn-xml-preview").WaitForAsync();
            await OpenBpmnToolTabAsync(page, "Versions");
            await page.GetByTestId("bpmn-version-compare").WaitForAsync();
            await OpenBpmnToolTabAsync(page, "Connectors");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Connector templates", Exact = true }).WaitForAsync();
            await OpenBpmnToolTabAsync(page, viewerTab);
        }
        else
        {
            await OpenBpmnToolTabAsync(page, viewerTab);
        }
        await page.GetByTestId(viewerTestId).WaitForAsync();
    }

    [Fact]
    public async Task BpmnModeler_AddNode_Produces_Valid_Roundtrip_Xml()
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await SelectFirstSequenceFlowAsync(page);
            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
            var catalog = page.GetByTestId("low-code-node-catalog");
            await catalog.GetByLabel("Search nodes").FillAsync("HTTP");
            await catalog.GetByRole(AriaRole.Button, new() { Name = "HTTP request", Exact = true }).ClickAsync();

            var xml = await WaitForPreviewXmlAsync(page, "serviceTask");
            var document = XDocument.Parse(xml);
            Assert.Contains(document.Descendants(), element => element.Name.LocalName == "serviceTask");
            Assert.Contains(document.Descendants(), element => element.Name.LocalName == "connector");

            await page.GetByRole(AriaRole.Button, new() { Name = "Validate", Exact = true }).ClickAsync();
            await page.GetByText("No issues", new() { Exact = true }).WaitForAsync();
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Theory]
    [InlineData("HTTP with retry", "retryPolicy", "connector", "serviceTask")]
    [InlineData("Webhook → IF → HTTP", "webhook", "exclusiveGateway", "connector")]
    [InlineData("Cron → Batch → DB", "timerEventDefinition", "multiInstanceLoopCharacteristics", "db-upsert")]
    [InlineData("User approval with form", "userTask", "formRef", "approval-form")]
    [InlineData("Decision table routing", "businessRuleTask", "decisionRef", "exclusiveGateway")]
    [InlineData("Case start from BPMN", "callActivity", "caseRef", "case-model")]
    public async Task BpmnModeler_Patterns_Create_Expected_Bpmn(string patternName, string firstToken, string secondToken, string thirdToken)
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
            await page.GetByTestId("low-code-node-catalog")
                .GetByRole(AriaRole.Button, new() { Name = patternName, Exact = true })
                .ClickAsync();

            var xml = await WaitForPreviewXmlAsync(page, firstToken);
            Assert.Contains(secondToken, xml, StringComparison.Ordinal);
            Assert.Contains(thirdToken, xml, StringComparison.Ordinal);
            XDocument.Parse(xml);
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Theory]
    [InlineData("Wait", "intermediateCatchEvent", "timerEventDefinition", "PT5M")]
    [InlineData("Subworkflow", "callActivity", "calledElement=\"subworkflow\"", "Call workflow")]
    public async Task BpmnModeler_Remaining_LowCodeMappings_Create_Expected_Bpmn(
        string nodeName,
        string firstToken,
        string secondToken,
        string thirdToken)
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await SelectFirstSequenceFlowAsync(page);
            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
            await page.GetByTestId("low-code-node-catalog")
                .GetByRole(AriaRole.Button, new() { Name = nodeName, Exact = true })
                .ClickAsync();

            var xml = await WaitForPreviewXmlAsync(page, firstToken);
            Assert.Contains(secondToken, xml, StringComparison.Ordinal);
            Assert.Contains(thirdToken, xml, StringComparison.Ordinal);
            XDocument.Parse(xml);
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task BpmnModeler_EventSubprocessCatalogEntry_RequiresPatternOrPalette()
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await SelectFirstSequenceFlowAsync(page);
            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
            await page.GetByTestId("low-code-node-catalog")
                .GetByRole(AriaRole.Button, new() { Name = "Error handler", Exact = true })
                .ClickAsync();

            await page.GetByText(
                "Error handler could not be inserted: Use a complete pattern or the BPMN palette",
                new() { Exact = false }).WaitForAsync();
            await OpenBpmnToolTabAsync(page, "XML");
            var xml = await page.GetByTestId("bpmn-xml-preview").GetByRole(AriaRole.Textbox).InputValueAsync();
            Assert.DoesNotContain("subProcess", xml, StringComparison.Ordinal);
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task BpmnModeler_QuickInsert_Splits_Selected_SequenceFlow()
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await ImportBpmnAsync(page, ImportableBpmn);
            var flow = page.GetByTestId("bpmn-modeler-shell").Locator(".djs-element[data-element-id='Flow_Import']");
            await flow.WaitForAsync(new() { State = WaitForSelectorState.Attached });
            await flow.Locator(".djs-hit").ClickAsync(new() { Force = true });
            await page.GetByTitle("Insert IF", new() { Exact = true }).ClickAsync();

            await OpenBpmnToolTabAsync(page, "XML");
            await page.GetByTestId("bpmn-xml-preview")
                .GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })
                .ClickAsync();
            var xml = await WaitForPreviewXmlAsync(page, "exclusiveGateway");
            var document = XDocument.Parse(xml);
            Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "sequenceFlow" && (string?)element.Attribute("id") == "Flow_Import");
            Assert.Equal(5, document.Descendants().Count(element => element.Name.LocalName == "sequenceFlow"));
            Assert.Equal(2, document.Descendants().Count(element => element.Name.LocalName == "exclusiveGateway"));
            Assert.Single(document.Descendants(), element => element.Name.LocalName == "task");
            var split = Assert.Single(document.Descendants(), element =>
                element.Name.LocalName == "exclusiveGateway" && element.Attribute("default") is not null);
            var defaultFlowId = (string?)split.Attribute("default");
            Assert.Contains(document.Descendants(), element =>
                element.Name.LocalName == "sequenceFlow" && (string?)element.Attribute("id") == defaultFlowId);
            Assert.Contains(document.Descendants(), element =>
                element.Name.LocalName == "sequenceFlow" && element.Elements().Any(child => child.Name.LocalName == "conditionExpression"));
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task BpmnModeler_ImportExport_Roundtrip_And_VersionCompare_Work_InBrowser()
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await ImportBpmnAsync(page, ImportableBpmn);
            var importedXml = await WaitForPreviewXmlAsync(page, "Process_Import");
            XDocument.Parse(importedXml);

            var download = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export XML", Exact = true }).ClickAsync());
            Assert.Equal("studio-model.bpmn", download.SuggestedFilename);
            var downloadedXml = await File.ReadAllTextAsync(await download.PathAsync(), TestContext.Current.CancellationToken);
            var downloadedDocument = XDocument.Parse(downloadedXml);
            Assert.Contains(downloadedDocument.Descendants(), element => element.Name.LocalName == "process" && (string?)element.Attribute("id") == "Process_Import");

            await OpenBpmnToolTabAsync(page, "Versions");
            await page.GetByTestId("bpmn-version-select").ClickAsync();
            await page.GetByRole(AriaRole.Option).First.ClickAsync();
            var compare = page.GetByTestId("bpmn-version-compare-action");
            await compare.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await compare.ClickAsync();
            await page.GetByText("Compared with deployed version:", new() { Exact = false }).WaitForAsync();

            Assert.Contains(host.ApiRequests, request => request.StartsWith("GET /api/repository/11111111-1111-1111-1111-111111111111", StringComparison.Ordinal));
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task BpmnModeler_LocalSimulation_IsAvailable_ThroughThePinnedTokenSimulationBundle()
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await OpenBpmnToolTabAsync(page, "Test runs");
            await page.GetByTestId("bpmn-local-simulation").WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Start simulation", Exact = true }).ClickAsync();
            await page.GetByText("Local token simulation started.", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Pause simulation", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Reset simulation", Exact = true }).ClickAsync();
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task BpmnModeler_EngineTestRun_DeploysAndStartsTheCurrentArtifact()
    {
        var (page, browserErrors) = await OpenBpmnModelerAsync();
        try
        {
            await OpenBpmnToolTabAsync(page, "Test runs");
            var testRun = page.GetByTestId("bpmn-engine-test-run");
            await testRun.GetByLabel("Test variables (JSON object)").FillAsync("{\"approved\":true}");
            await testRun.GetByRole(AriaRole.Button, new() { Name = "Deploy and run test", Exact = true }).ClickAsync();
            await page.GetByText("Engine test run verified: the completed state.", new() { Exact = true }).WaitForAsync();
            await testRun.GetByText("77777777-7777-7777-7777-777777777777", new() { Exact = false }).WaitForAsync();

            Assert.Contains(host.ApiRequests, request => request.StartsWith("POST /api/repository", StringComparison.Ordinal));
            Assert.Contains(host.ApiRequests, request => request.StartsWith("POST /api/runtime/start", StringComparison.Ordinal));
            Assert.Empty(browserErrors);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task<(IPage Page, ConcurrentQueue<string> BrowserErrors)> OpenBpmnModelerAsync()
    {
        var page = await host.Browser.NewPageAsync();
        var browserErrors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        await page.GotoAsync($"{host.BaseAddress}bpmn-modeler");
        var shell = page.GetByTestId("bpmn-modeler-shell");
        await shell.WaitForAsync();
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (await page.Locator("[data-modeler-ready='true'] .djs-container").CountAsync() > 0)
                return (page, browserErrors);
            await Task.Delay(250);
        }

        throw new InvalidOperationException(
            $"The bpmn.io modeler did not initialize. Browser errors: {string.Join(" | ", browserErrors)}. " +
            $"Studio logs: {string.Join(" | ", host.StudioLogs)}. Shell HTML: {await shell.InnerHTMLAsync()}");
    }

    private static async Task SelectFirstSequenceFlowAsync(IPage page)
    {
        var flow = page.GetByTestId("bpmn-modeler-shell").Locator(".djs-connection .djs-hit").First;
        await flow.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await flow.ClickAsync(new() { Force = true });
    }

    private static async Task ImportBpmnAsync(IPage page, string xml)
    {
        await page.GetByTestId("bpmn-import-file").SetInputFilesAsync(new FilePayload
        {
            Name = "imported.bpmn",
            MimeType = "application/xml",
            Buffer = Encoding.UTF8.GetBytes(xml)
        });
        await page.GetByText("Imported imported.bpmn.", new() { Exact = true }).WaitForAsync();
    }

    private async Task<string> WaitForPreviewXmlAsync(IPage page, string expectedToken)
    {
        await OpenBpmnToolTabAsync(page, "XML");
        var preview = page.GetByTestId("bpmn-xml-preview").GetByRole(AriaRole.Textbox);
        var lastXml = string.Empty;
        for (var attempt = 0; attempt < 120; attempt++)
        {
            lastXml = await preview.InputValueAsync();
            if (lastXml.Contains(expectedToken, StringComparison.Ordinal))
                return lastXml;
            await Task.Delay(250);
        }

        var notifications = await page.Locator(".mud-snackbar").AllTextContentsAsync();
        throw new TimeoutException(
            $"The BPMN XML preview did not contain '{expectedToken}'. Notifications: {string.Join(" | ", notifications)}. Last XML: {lastXml}");
    }

    private async Task OpenBpmnToolTabAsync(IPage page, string name)
    {
        var tab = page.GetByRole(AriaRole.Tab, new() { Name = name, Exact = true });
        await tab.ClickAsync();
        await page.WaitForFunctionAsync(
            "name => [...document.querySelectorAll('[role=tab]')].some(element => element.textContent?.trim() === name && element.getAttribute('aria-selected') === 'true')",
            name);
    }
}
