using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

// Real modeler import/deploy and inbox operations; API calls provide setup and
// independent persisted-state assertions. These are NOT graphical drawing tests.
public sealed partial class LocalStudioInfrastructureTests
{
    [Theory]
    [InlineData(5000, "path-a", "flow-a")]
    [InlineData(10, "path-b", "flow-default")]
    [Trait("UseCase", "UC02")]
    public async Task BrowserWorkflow_ExclusiveGateway_DeploysRoundtripAndCompletesOnlySelectedTask(
        int amount, string expectedTaskId, string expectedFlowId)
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E only.");
        var key = $"BrowserXor_{amount}_{host.RunId}";
        var errors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync($"{nameof(BrowserWorkflow_ExclusiveGateway_DeploysRoundtripAndCompletesOnlySelectedTask)}--{amount}");
        page.PageError += (_, error) => errors.Enqueue(error);
        using var client = host.CreateApiClient();
        try
        {
            await ImportExportDeployWorkflowAsync(page, client, key,
                AddBrowserDiagram(CreateExclusiveGatewayBpmn(key, "${amount > 1000}")));
            var id = await StartProcessWithVariablesAsync(client, key, null, key,
                new Dictionary<string, object> { ["amount"] = amount });
            await WaitForOpenTaskCountAsync(id, 1);
            var task = Assert.Single(await GetOpenTasksAsync(id));
            Assert.Equal(expectedTaskId, task.GetProperty("activityId").GetString());
            await CompleteInboxTaskThroughBrowserAsync(page, task);
            await WaitForInstanceStateAsync(id, "Completed");
            var history = await GetHistoryAsync(id);
            Assert.Equal(expectedFlowId, GetGatewaySelectedFlowId(history, "EXCLUSIVE_GATEWAY_SELECTED"));
            Assert.DoesNotContain(history, e => EventHasElementId(e, expectedTaskId == "path-a" ? "path-b" : "path-a"));
            Assert.Empty(errors);
        }
        finally { await host.ClosePageAsync(page); }
    }

    [Theory]
    [InlineData("parallel", 2)]
    [InlineData("inclusive", 2)]
    [InlineData("multi-parallel", 3)]
    [InlineData("multi-sequential", 1)]
    [InlineData("embedded", 1)]
    [Trait("UseCase", "WORKFLOW-WAITING")]
    public async Task BrowserWorkflow_WaitingActivities_RoundtripAndInboxCompletionPreserveJoinSemantics(
        string variant, int initiallyOpen)
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E only.");
        var key = $"BrowserWait_{variant.Replace('-', '_')}_{host.RunId}";
        var errors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync($"{nameof(BrowserWorkflow_WaitingActivities_RoundtripAndInboxCompletionPreserveJoinSemantics)}--{variant}");
        page.PageError += (_, error) => errors.Enqueue(error);
        using var client = host.CreateApiClient();
        try
        {
            var xml = variant switch
            {
                "parallel" => CreateParallelGatewayBpmn(key),
                "inclusive" => CreateInclusiveGatewayBpmn(key),
                _ => BuildBrowserWaitingBpmn(key, variant)
            };
            await ImportExportDeployWorkflowAsync(page, client, key, AddBrowserDiagram(xml));
            var id = await StartProcessWithVariablesAsync(client, key, null, key,
                new Dictionary<string, object> { ["x"] = true, ["y"] = true,
                    ["approvers"] = new[] { "Alice", "Bob", "Carol" } });
            await WaitForOpenTaskCountAsync(id, initiallyOpen);
            var initialTasks = await GetOpenTasksAsync(id);
            if (variant == "inclusive")
                Assert.Equal(new[] { "inc-task-a", "inc-task-b" }, initialTasks.Select(t => t.GetProperty("activityId").GetString()).Order().ToArray());
            if (variant == "multi-parallel")
                Assert.Equal(new[] { "Alice", "Bob", "Carol" }, initialTasks.Select(t =>
                    t.GetProperty("localVariables").GetProperty("approver").GetString()).Order().ToArray());

            var total = variant.StartsWith("multi-", StringComparison.Ordinal) ? 3 : initiallyOpen;
            var completedIds = new HashSet<Guid>();
            for (var iteration = 0; iteration < total; iteration++)
            {
                await WaitForOpenTaskCountAsync(id, variant == "multi-sequential" ? 1 : total - iteration);
                var open = await GetOpenTasksAsync(id);
                var task = open[0];
                Assert.True(completedIds.Add(task.GetProperty("id").GetGuid()), "Every iteration must be a distinct task.");
                await CompleteInboxTaskThroughBrowserAsync(page, task);
                if (iteration < total - 1)
                {
                    Assert.NotEqual("Completed", await GetInstanceStateAsync(id));
                    Assert.DoesNotContain(await GetHistoryAsync(id), e => EventHasElementId(e, "end") && IsEndEventReached(e));
                }
            }
            await WaitForInstanceStateAsync(id, "Completed");
            var history = await GetHistoryAsync(id);
            Assert.Equal(total, history.Count(e => IsEventType(e, "USER_TASK_COMPLETED")));
            if (variant == "inclusive")
                Assert.DoesNotContain(history, e => EventHasElementId(e, "inc-task-c"));
            if (variant.StartsWith("multi-", StringComparison.Ordinal))
                Assert.Equal(3, history.Count(e => EventHasElementId(e, "review") && IsEventType(e, "USER_TASK_CREATED")));
            Assert.Empty(errors);
        }
        finally { await host.ClosePageAsync(page); }
    }

    [Fact]
    [Trait("UseCase", "UC08-CALL")]
    public async Task BrowserWorkflow_CallActivity_PersistsSeparateChildAndContinuesParentOnlyAfterChildCompletion()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E only.");
        var parentKey = $"BrowserParent_{host.RunId}";
        var childKey = $"BrowserChild_{host.RunId}";
        var page = await host.CreatePageAsync();
        using var client = host.CreateApiClient();
        try
        {
            await ImportExportDeployWorkflowAsync(page, client, childKey,
                AddBrowserDiagram(BuildBrowserWaitingBpmn(childKey, "single")));
            var parentXml = $$"""
                <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="urn:browser:call">
                  <process id="{{parentKey}}" isExecutable="true">
                    <startEvent id="start"/><sequenceFlow id="f1" sourceRef="start" targetRef="call"/>
                    <callActivity id="call" calledElement="{{childKey}}"/>
                    <sequenceFlow id="f2" sourceRef="call" targetRef="parent-review"/>
                    <userTask id="parent-review" name="Parent continuation"/>
                    <sequenceFlow id="f3" sourceRef="parent-review" targetRef="end"/><endEvent id="end"/>
                  </process>
                </definitions>
                """;
            await ImportExportDeployWorkflowAsync(page, client, parentKey, AddBrowserDiagram(parentXml));
            var parent = await StartProcessWithVariablesAsync(client, parentKey, null, parentKey, new Dictionary<string, object>());
            Assert.Empty(await GetOpenTasksAsync(parent));
            using var allTasksResponse = await client.GetAsync("api/task", TestContext.Current.CancellationToken);
            allTasksResponse.EnsureSuccessStatusCode();
            var allTasks = await allTasksResponse.Content.ReadFromJsonAsync<JsonElement[]>(TestContext.Current.CancellationToken);
            var childTask = Assert.Single(allTasks ?? [], t => t.GetProperty("name").GetString() == $"Review {childKey}");
            var child = childTask.GetProperty("processInstanceId").GetGuid();
            Assert.NotEqual(parent, child);
            using var childResponse = await client.GetAsync($"api/runtime/{child}", TestContext.Current.CancellationToken);
            childResponse.EnsureSuccessStatusCode();
            var childInstance = await childResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Equal(parent, childInstance.GetProperty("parentProcessInstanceId").GetGuid());
            Assert.Equal("call", childInstance.GetProperty("callingActivityId").GetString());
            using var parentBefore = await client.GetAsync($"api/runtime/{parent}", TestContext.Current.CancellationToken);
            parentBefore.EnsureSuccessStatusCode();
            var before = await parentBefore.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.Contains(before.GetProperty("activeTokens").EnumerateArray(), token => token.GetString() == "call");
            await CompleteInboxTaskThroughBrowserAsync(page, childTask);
            await WaitForInstanceStateAsync(child, "Completed");
            await WaitForOpenTaskCountAsync(parent, 1);
            var parentTask = Assert.Single(await GetOpenTasksAsync(parent));
            Assert.Equal("parent-review", parentTask.GetProperty("activityId").GetString());
            await CompleteInboxTaskThroughBrowserAsync(page, parentTask);
            await WaitForInstanceStateAsync(parent, "Completed");
        }
        finally { await host.ClosePageAsync(page); }
    }

    [Fact]
    [Trait("UseCase", "UC10-PIN")]
    public async Task BrowserWorkflow_VersionDeployment_PinsRunningInstanceAndUsesV2ForNewInstance()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E only.");
        var key = $"BrowserPinned_{host.RunId}";
        var page = await host.CreatePageAsync();
        using var client = host.CreateApiClient();
        try
        {
            var v1 = AddBrowserDiagram(BuildBrowserWaitingBpmn(key, "single"));
            await ImportExportDeployWorkflowAsync(page, client, key, v1);
            var oldId = await StartProcessWithVariablesAsync(client, key, null, $"{key}-v1", new Dictionary<string, object>());
            await WaitForOpenTaskCountAsync(oldId, 1);
            var oldTask = Assert.Single(await GetOpenTasksAsync(oldId));
            // A distinct valid v2 test model; no existing conformance fixture is altered.
            var v2 = XDocument.Parse(v1);
            var task = v2.Descendants().Single(e => e.Name.LocalName == "userTask");
            task.SetAttributeValue("id", "review-v2");
            task.SetAttributeValue("name", $"Version two {key}");
            foreach (var attribute in v2.Descendants().Attributes().Where(a =>
                         (a.Name.LocalName is "sourceRef" or "targetRef" or "bpmnElement") && a.Value == "review"))
                attribute.Value = "review-v2";
            await ImportExportDeployWorkflowAsync(page, client, key, v2.ToString(), expectedVersion: 2);
            var newId = await StartProcessWithVariablesAsync(client, key, null, $"{key}-v2", new Dictionary<string, object>());
            await WaitForOpenTaskCountAsync(newId, 1);
            Assert.Equal("review", Assert.Single(await GetOpenTasksAsync(oldId)).GetProperty("activityId").GetString());
            var newTask = Assert.Single(await GetOpenTasksAsync(newId));
            Assert.Equal("review-v2", newTask.GetProperty("activityId").GetString());
            using var oldResponse = await client.GetAsync($"api/runtime/{oldId}", TestContext.Current.CancellationToken);
            using var newResponse = await client.GetAsync($"api/runtime/{newId}", TestContext.Current.CancellationToken);
            oldResponse.EnsureSuccessStatusCode(); newResponse.EnsureSuccessStatusCode();
            var oldInstance = await oldResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var newInstance = await newResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.NotEqual(oldInstance.GetProperty("processDefinitionId").GetGuid(), newInstance.GetProperty("processDefinitionId").GetGuid());
            await CompleteInboxTaskThroughBrowserAsync(page, oldTask);
            await CompleteInboxTaskThroughBrowserAsync(page, newTask);
            await WaitForInstanceStateAsync(oldId, "Completed");
            await WaitForInstanceStateAsync(newId, "Completed");
            Assert.DoesNotContain(await GetHistoryAsync(oldId), e => EventHasElementId(e, "review-v2"));
        }
        finally { await host.ClosePageAsync(page); }
    }

    [Fact]
    [Trait("UseCase", "ADMIN-CSV")]
    public async Task Analytics_TrainingCsvDownload_ContainsCompletedInstancesAndExcludesOtherTenantAndProcess()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E only.");
        var page = await host.CreatePageAsync();
        using var client = host.CreateApiClient();
        var tenantName = $"CSV owner {host.RunId}";
        var key = $"BrowserCsv_{host.RunId}";
        var otherKey = $"BrowserOtherCsv_{host.RunId}";
        try
        {
            var tenant = await CreateTenantAsync(client, tenantName);
            var otherTenant = await CreateTenantAsync(client, $"CSV other {host.RunId}");
            host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenant)}");
            host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(otherTenant)}");
            foreach (var (processKey, tenantId) in new[] { (key, tenant), (key, otherTenant), (otherKey, tenant) })
            {
                host.RegisterProcessDefinitionCleanup(processKey, tenantId);
                await DeployProcessAsync(client, processKey, tenantId, processKey);
            }
            var included = await StartProcessWithVariablesAsync(client, key, tenant, $"{key}-included", new Dictionary<string, object>());
            var excludedTenant = await StartProcessWithVariablesAsync(client, key, otherTenant, $"{key}-foreign", new Dictionary<string, object>());
            var excludedKey = await StartProcessWithVariablesAsync(client, otherKey, tenant, otherKey, new Dictionary<string, object>());
            foreach (var id in new[] { included, excludedTenant, excludedKey }) await WaitForInstanceStateAsync(id, "Completed");
            // The mining writer is asynchronous. Poll the real export until its completed row exists,
            // then assert the independent browser download (never treat an error banner as success).
            var deadline = DateTime.UtcNow.AddSeconds(30);
            var sourceCsv = string.Empty;
            while (DateTime.UtcNow < deadline)
            {
                using var response = await client.GetAsync($"api/ml/export/training-data?tenantId={Uri.EscapeDataString(tenant)}&processDefinitionKey={key}", TestContext.Current.CancellationToken);
                response.EnsureSuccessStatusCode();
                sourceCsv = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                if (sourceCsv.Contains(included.ToString(), StringComparison.Ordinal)) break;
                await Task.Delay(250, TestContext.Current.CancellationToken);
            }
            Assert.Contains(included.ToString(), sourceCsv, StringComparison.Ordinal);
            await page.GotoAsync($"{host.StudioBaseAddress}analytics");
            await SelectTenantAsync(page, tenantName, tenant);
            await FillBoundInputAsync(page.GetByLabel("Process definition key (optional)", new() { Exact = true }), key);
            var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Button,
                new() { Name = "Trainingsdaten exportieren", Exact = true }).ClickAsync());
            Assert.EndsWith(".csv", download.SuggestedFilename, StringComparison.Ordinal);
            Assert.Null(await download.FailureAsync());
            var csv = await File.ReadAllTextAsync((await download.PathAsync())!, TestContext.Current.CancellationToken);
            Assert.StartsWith("tenantId,processDefinitionKey,processInstanceId,", csv, StringComparison.Ordinal);
            Assert.Contains(included.ToString(), csv, StringComparison.Ordinal);
            Assert.DoesNotContain(excludedTenant.ToString(), csv, StringComparison.Ordinal);
            Assert.DoesNotContain(excludedKey.ToString(), csv, StringComparison.Ordinal);
            Assert.Equal(2, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            await page.GetByText("Trainingsdaten wurden exportiert.", new() { Exact = true }).WaitForAsync();
        }
        finally { await host.ClosePageAsync(page); }
    }

    private async Task ImportExportDeployWorkflowAsync(IPage page, HttpClient client,
        string key, string xml, int expectedVersion = 1)
    {
        host.RegisterProcessDefinitionCleanup(key);
        await OpenBpmnModelerAsync(page);
        await ImportBpmnAsync(page, xml);
        // Actual visible diagram, not just an XML preview or parser assertion.
        await page.GetByTestId("bpmn-modeler-shell").Locator(".djs-shape .djs-hit").First.WaitForAsync();
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Button,
            new() { Name = "Export XML", Exact = true }).ClickAsync());
        Assert.Null(await download.FailureAsync());
        var exported = await File.ReadAllTextAsync((await download.PathAsync())!, TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowSemantics(xml), WorkflowSemantics(exported));
        await ImportBpmnAsync(page, exported);
        await page.GetByRole(AriaRole.Button, new() { Name = "Validate", Exact = true }).ClickAsync();
        await page.GetByText("No issues", new() { Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
        await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();
        var versions = await client.GetFromJsonAsync<JsonElement[]>($"api/repository?key={key}", TestContext.Current.CancellationToken);
        var persisted = Assert.Single(versions ?? [], v => v.GetProperty("version").GetInt32() == expectedVersion);
        Assert.Equal(WorkflowSemantics(exported), WorkflowSemantics(persisted.GetProperty("bpmnXml").GetString()!));
    }

    private async Task CompleteInboxTaskThroughBrowserAsync(IPage page, JsonElement task)
    {
        var id = task.GetProperty("id").GetGuid();
        await page.GotoAsync($"{host.StudioBaseAddress}tasks");
        await FillBoundInputAsync(page.GetByPlaceholder("Search tasks", new() { Exact = true }), task.GetProperty("processInstanceId").GetGuid().ToString());
        var row = page.Locator("tbody tr").Filter(new() { HasText = id.ToString() });
        await Assertions.Expect(row).ToHaveCountAsync(1);
        await row.GetByRole(AriaRole.Button, new() { Name = "Claim Task", Exact = true }).ClickAsync();
        await page.GetByText($"Task {id} claimed!", new() { Exact = true }).WaitForAsync();
        await row.GetByRole(AriaRole.Button, new() { Name = "Complete Task", Exact = true }).ClickAsync();
        await page.GetByText($"Task {id} completed!", new() { Exact = true }).WaitForAsync();
        await Assertions.Expect(row).ToHaveCountAsync(0);
    }

    // Namespace-aware structural fingerprint includes scopes, attributes and leaf
    // content; ignores formatting/DI, derived inverse refs and xsi:type annotations.
    private static string WorkflowSemantics(string xml)
    {
        var process = XDocument.Parse(xml).Descendants().Single(e => e.Name.LocalName == "process");
        // incoming/outgoing are redundant inverse references derived from sequenceFlow.
        // Moddle may add them or reorder siblings; neither changes process semantics.
        return string.Join('\n', process.DescendantsAndSelf()
            .Where(e => e.Name.LocalName is not ("incoming" or "outgoing"))
            .Select(e => $"{string.Join('/', e.Ancestors().Where(a => a.Name.LocalName != "definitions")
                .Reverse().Select(a => $"{a.Name}:{(string?)a.Attribute("id")}"))}/{e.Name}|{string.Join(';', e.Attributes()
                .Where(a => !a.IsNamespaceDeclaration && a.Name != XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"))
                .OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => $"{a.Name}={a.Value}"))}|{(e.HasElements ? string.Empty : e.Value.Trim())}")
            .Order(StringComparer.Ordinal));
    }

    private static string BuildBrowserWaitingBpmn(string key, string variant)
    {
        var activity = variant == "embedded"
            ? $"<subProcess id='scope'><startEvent id='inner-start'/><sequenceFlow id='i1' sourceRef='inner-start' targetRef='review'/><userTask id='review' name='Review {key}'/><sequenceFlow id='i2' sourceRef='review' targetRef='inner-end'/><endEvent id='inner-end'/></subProcess>"
            : $"<userTask id='review' name='Review {key}'>" + (variant.StartsWith("multi-", StringComparison.Ordinal)
                ? $"<multiInstanceLoopCharacteristics isSequential='{(variant == "multi-sequential" ? "true" : "false")}' camunda:collection='approvers' camunda:elementVariable='approver'/>" : "") + "</userTask>";
        var activityId = variant == "embedded" ? "scope" : "review";
        return $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn' targetNamespace='urn:browser:waiting'><process id='{key}' isExecutable='true'><startEvent id='start'/><sequenceFlow id='f1' sourceRef='start' targetRef='{activityId}'/>{activity}<sequenceFlow id='f2' sourceRef='{activityId}' targetRef='end'/><endEvent id='end'/></process></definitions>";
    }

    // New browser fixtures need DI; only add presentation data, never change executable XML.
    // Expanded subprocess children are placed inside their parent shape.
    private static string AddBrowserDiagram(string xml)
    {
        var document = XDocument.Parse(xml);
        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        XNamespace bpmndi = "http://www.omg.org/spec/BPMN/20100524/DI";
        XNamespace dc = "http://www.omg.org/spec/DD/20100524/DC";
        XNamespace di = "http://www.omg.org/spec/DD/20100524/DI";
        var process = document.Descendants(bpmn + "process").Single();
        var plane = new XElement(bpmndi + "BPMNPlane", new XAttribute("id", "BrowserPlane"), new XAttribute("bpmnElement", (string)process.Attribute("id")!));
        var positions = new Dictionary<string, (int X, int Y, int W, int H)>();
        void AddScope(XElement scope, int offsetX, int offsetY)
        {
            var index = 0;
            foreach (var node in scope.Elements().Where(e => e.Name.Namespace == bpmn && e.Name.LocalName != "sequenceFlow" && e.Attribute("id") is not null))
            {
                var isScope = node.Name.LocalName == "subProcess";
                var activity = node.Name.LocalName.EndsWith("Task", StringComparison.Ordinal) || node.Name.LocalName == "callActivity";
                var x = offsetX + index++ * 160;
                var y = offsetY;
                var w = isScope ? 480 : activity ? 100 : 50;
                var h = isScope ? 220 : activity ? 80 : 50;
                var id = (string)node.Attribute("id")!;
                positions[id] = (x, y, w, h);
                var shape = new XElement(bpmndi + "BPMNShape", new XAttribute("id", $"{id}_di"), new XAttribute("bpmnElement", id),
                    new XElement(dc + "Bounds", new XAttribute("x", x), new XAttribute("y", y), new XAttribute("width", w), new XAttribute("height", h)));
                if (isScope) shape.SetAttributeValue("isExpanded", "true");
                plane.Add(shape);
                if (isScope) { AddScope(node, x + 35, y + 70); index += 3; }
            }
        }
        AddScope(process, 100, 100);
        foreach (var flow in process.Descendants(bpmn + "sequenceFlow"))
        {
            var from = positions[(string)flow.Attribute("sourceRef")!];
            var to = positions[(string)flow.Attribute("targetRef")!];
            plane.Add(new XElement(bpmndi + "BPMNEdge", new XAttribute("id", $"{(string)flow.Attribute("id")!}_di"), new XAttribute("bpmnElement", (string)flow.Attribute("id")!),
                new XElement(di + "waypoint", new XAttribute("x", from.X + from.W), new XAttribute("y", from.Y + from.H / 2)),
                new XElement(di + "waypoint", new XAttribute("x", to.X), new XAttribute("y", to.Y + to.H / 2))));
        }
        document.Root!.Add(new XElement(bpmndi + "BPMNDiagram", new XAttribute("id", "BrowserDiagram"), plane));
        return document.ToString();
    }
}
