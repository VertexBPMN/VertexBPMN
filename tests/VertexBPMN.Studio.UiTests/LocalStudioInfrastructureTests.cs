using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

[Trait("Category", "LocalStudioE2E")]
public sealed class LocalStudioInfrastructureTests(LocalStudioE2ETestHost host)
    : IClassFixture<LocalStudioE2ETestHost>
{
    private string ProcessKey => $"StudioE2E_{host.RunId}";

    [Fact]
    public async Task RealApiAndStudio_BecomeReady_AndDashboardUsesTheRealBackend()
    {
        Assert.SkipUnless(
            LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        using var apiClient = new HttpClient { BaseAddress = host.ApiBaseAddress };
        using var readiness = await apiClient.GetAsync("api/ready", TestContext.Current.CancellationToken);
        var readinessBody = await readiness.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(readiness.IsSuccessStatusCode, readinessBody);
        Assert.Equal(5, host.IsolatedDatabaseNames.Count);
        Assert.All(
            host.IsolatedDatabaseNames,
            databaseName => Assert.EndsWith($"_e2e_{host.RunId}", databaseName, StringComparison.Ordinal));

        var browserErrors = new ConcurrentQueue<string>();
        var failedRequests = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.RequestFailed += (_, request) => failedRequests.Enqueue(
            $"{request.Method} {request.Url}: {request.Failure}");

        try
        {
            var response = await page.GotoAsync(host.StudioBaseAddress.ToString());
            Assert.NotNull(response);
            Assert.True(response.Ok, $"Studio returned HTTP {response.Status}.");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
            await page.GetByText("VertexBPMN Studio", new() { Exact = true }).First.WaitForAsync();

            Assert.True(
                browserErrors.IsEmpty,
                $"Browser errors: {string.Join(" | ", browserErrors)}. Recent Studio logs: {string.Join(" | ", host.StudioLogs.TakeLast(150))}");
            Assert.Empty(failedRequests);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task BpmnModeler_ImportsEditsDeploysReloadsAndExports_ARealPersistedDefinition()
    {
        Assert.SkipUnless(
            LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        host.RegisterProcessDefinitionCleanup(ProcessKey);

        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                browserErrors.Enqueue($"console: {message.Text}");
        };

        try
        {
            await OpenBpmnModelerAsync(page);
            await ImportBpmnAsync(page, CreateBpmn(ProcessKey));

            var originalFlowId = $"Flow_{ProcessKey}";
            var originalFlow = page.GetByTestId("bpmn-modeler-shell")
                .Locator($".djs-element[data-element-id='{originalFlowId}']");
            await originalFlow.Locator(".djs-hit").ClickAsync(new() { Force = true });
            await page.GetByTitle("Insert HTTP", new() { Exact = true }).ClickAsync();
            await page.GetByTestId("bpmn-xml-preview")
                .GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })
                .ClickAsync();

            var editedXml = await WaitForPreviewXmlAsync(page, "serviceTask");
            var editedDocument = XDocument.Parse(editedXml);
            Assert.Contains(
                editedDocument.Descendants(),
                element => element.Name.LocalName == "process"
                           && (string?)element.Attribute("id") == ProcessKey);
            Assert.Contains(editedDocument.Descendants(), element => element.Name.LocalName == "serviceTask");
            Assert.DoesNotContain(
                editedDocument.Descendants(),
                element => element.Name.LocalName == "sequenceFlow"
                           && (string?)element.Attribute("id") == originalFlowId);
            Assert.Equal(2, editedDocument.Descendants().Count(element => element.Name.LocalName == "sequenceFlow"));
            Assert.Contains(
                editedDocument.Descendants(),
                element => element.Name.LocalName == "connector"
                           && (string?)element.Attribute("type") == "http");

            var serviceTaskId = editedDocument.Descendants()
                .Single(element => element.Name.LocalName == "serviceTask")
                .Attribute("id")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(serviceTaskId));
            await page.GetByTestId("bpmn-modeler-shell")
                .Locator($".djs-element[data-element-id='{serviceTaskId}'] .djs-hit")
                .ClickAsync(new() { Force = true });
            var propertiesPanel = page.GetByLabel("BPMN properties panel", new() { Exact = true });
            await propertiesPanel.GetByText("Vertex", new() { Exact = true }).ClickAsync();
            await FillBoundInputAsync(
                propertiesPanel.GetByRole(AriaRole.Textbox, new() { Name = "Credential ref", Exact = true }),
                $"credential-{host.RunId}");
            await page.GetByTestId("bpmn-xml-preview")
                .GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })
                .ClickAsync();
            editedXml = await WaitForPreviewXmlAsync(page, $"credential-{host.RunId}");
            editedDocument = XDocument.Parse(editedXml);

            await page.GetByRole(AriaRole.Button, new() { Name = "Validate", Exact = true }).ClickAsync();
            await page.GetByText("No issues", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
            await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();

            using var apiClient = host.CreateApiClient();
            var definitions = await apiClient.GetFromJsonAsync<JsonElement[]>(
                $"api/repository?key={Uri.EscapeDataString(ProcessKey)}",
                TestContext.Current.CancellationToken);
            var persisted = Assert.Single(definitions ?? []);
            Assert.Equal(ProcessKey, persisted.GetProperty("key").GetString());
            Assert.Contains("serviceTask", persisted.GetProperty("bpmnXml").GetString(), StringComparison.Ordinal);

            await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
            await FillBoundInputAsync(page.GetByTestId("process-definition-search"), ProcessKey);
            var processGridText = await page.Locator("table").InnerTextAsync();
            Assert.Contains(ProcessKey, processGridText, StringComparison.Ordinal);
            await OpenBpmnModelerAsync(page);

            await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Option).Filter(new() { HasText = ProcessKey }).First.ClickAsync();
            await page.GetByTestId("bpmn-version-load-action").ClickAsync();
            await page.GetByText("Deployed BPMN version loaded into the editor.", new() { Exact = true }).WaitForAsync();
            var reloadedXml = await WaitForPreviewXmlAsync(page, ProcessKey);
            Assert.Contains("serviceTask", reloadedXml, StringComparison.Ordinal);
            XDocument.Parse(reloadedXml);

            var download = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export XML", Exact = true }).ClickAsync());
            var downloadedXml = await File.ReadAllTextAsync(
                await download.PathAsync(),
                TestContext.Current.CancellationToken);
            Assert.Contains("serviceTask", downloadedXml, StringComparison.Ordinal);
            Assert.Contains(
                XDocument.Parse(downloadedXml).Descendants(),
                element => element.Name.LocalName == "process"
                           && (string?)element.Attribute("id") == ProcessKey);

            await ImportBpmnAsync(page, downloadedXml);
            var roundtripXml = await WaitForPreviewXmlAsync(page, ProcessKey);
            var roundtripDocument = XDocument.Parse(roundtripXml);
            Assert.Equal(
                editedDocument.Descendants().Count(element => element.Name.LocalName == "serviceTask"),
                roundtripDocument.Descendants().Count(element => element.Name.LocalName == "serviceTask"));
            Assert.Equal(
                editedDocument.Descendants().Count(element => element.Name.LocalName == "sequenceFlow"),
                roundtripDocument.Descendants().Count(element => element.Name.LocalName == "sequenceFlow"));

            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
            var catalog = page.GetByTestId("low-code-node-catalog");
            await catalog.GetByLabel("Search nodes").FillAsync("Decision");
            await catalog.GetByRole(AriaRole.Button, new() { Name = "Decision table", Exact = true }).ClickAsync();
            await WaitForPreviewXmlAsync(page, "businessRuleTask");
            await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
            catalog = page.GetByTestId("low-code-node-catalog");
            await catalog.GetByLabel("Search nodes").FillAsync("User approval form");
            await catalog.GetByRole(AriaRole.Button, new() { Name = "User approval form", Exact = true }).ClickAsync();
            var configuredXml = await WaitForPreviewXmlAsync(page, "formRef=\"approval-form\"");
            Assert.Contains("decisionRef=\"decision-table\"", configuredXml, StringComparison.Ordinal);
            await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
            await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).Last.WaitForAsync();

            var versions = await apiClient.GetFromJsonAsync<JsonElement[]>(
                $"api/repository?key={Uri.EscapeDataString(ProcessKey)}",
                TestContext.Current.CancellationToken);
            Assert.Equal([1, 2], (versions ?? []).Select(version => version.GetProperty("version").GetInt32()).Order().ToArray());

            await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Option).Filter(new() { HasText = $"v1, {ProcessKey}" }).ClickAsync();
            await page.GetByTestId("bpmn-version-compare-action").ClickAsync();
            await page.GetByText("Compared with deployed version:", new() { Exact = false }).WaitForAsync();

            await page.GetByTestId("bpmn-version-load-action").ClickAsync();
            var versionOneXml = await WaitForPreviewXmlWithoutAsync(page, ProcessKey, "businessRuleTask");
            Assert.DoesNotContain("businessRuleTask", versionOneXml, StringComparison.Ordinal);

            await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Option).Filter(new() { HasText = $"v2, {ProcessKey}" }).ClickAsync();
            await page.GetByTestId("bpmn-version-load-action").ClickAsync();
            var versionTwoXml = await WaitForPreviewXmlAsync(page, "businessRuleTask");
            Assert.Contains(ProcessKey, versionTwoXml, StringComparison.Ordinal);
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task BpmnModeler_SimulatesAndDeploysAndRuns_TheCurrentRealArtifact()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var processKey = $"StudioE2E_Run_{host.RunId}";
        host.RegisterProcessDefinitionCleanup(processKey);
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                browserErrors.Enqueue($"console: {message.Text}");
        };

        try
        {
            await OpenBpmnModelerAsync(page);
            await ImportBpmnAsync(page, CreateBpmn(processKey));
            await page.GetByRole(AriaRole.Button, new() { Name = "Start simulation", Exact = true }).ClickAsync();
            await page.GetByText("Local token simulation started.", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Pause simulation", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Reset simulation", Exact = true }).ClickAsync();

            var engineRun = page.GetByTestId("bpmn-engine-test-run");
            await FillBoundInputAsync(engineRun.GetByLabel("Test variables (JSON object)", new() { Exact = true }), "{\"approved\":true,\"source\":\"local-gui-e2e\"}");
            await engineRun.GetByRole(AriaRole.Button, new() { Name = "Deploy and run test", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(page, "Engine test run verified: the completed state.", browserErrors, "BPMN engine test run");
            await engineRun.GetByText("reached the completed state", new() { Exact = false }).WaitForAsync();

            using var apiClient = host.CreateApiClient();
            var instances = await apiClient.GetFromJsonAsync<JsonElement[]>("api/runtime", TestContext.Current.CancellationToken);
            var instance = Assert.Single(instances ?? [], candidate => candidate.GetProperty("processId").GetString() == processKey);
            Assert.Equal("Completed", instance.GetProperty("state").GetString());
            Assert.True(instance.GetProperty("variables").GetProperty("approved").GetBoolean());
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task BpmnRuntime_StartsClaimsCompletesAndShowsPersistedHistory_WithARealTaskForm()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var tenantName = $"Runtime E2E {host.RunId}";
        var processKey = $"StudioE2E_Task_{host.RunId}";
        var formKey = $"studio-e2e-task-form-{host.RunId}";
        var businessKey = $"business-{host.RunId}";
        string? tenantId = null;
        string? formId = null;
        using var apiClient = host.CreateApiClient();
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                browserErrors.Enqueue($"console: {message.Text}");
        };

        try
        {
            using (var tenantResponse = await apiClient.PostAsJsonAsync("api/tenant", new { name = tenantName, description = "Runtime browser E2E" }, TestContext.Current.CancellationToken))
            {
                var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
                using var tenant = JsonDocument.Parse(tenantBody);
                tenantId = tenant.RootElement.GetProperty("id").GetString();
                Assert.False(string.IsNullOrWhiteSpace(tenantId));
                host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
            }

            host.RegisterProcessDefinitionCleanup(processKey, tenantId);

            using (var formResponse = await apiClient.PostAsJsonAsync("api/forms", new { tenantId, key = formKey, name = "Runtime approval form", schema = CreateFormJson(host.RunId) }, TestContext.Current.CancellationToken))
            {
                var formBody = await formResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.True(formResponse.IsSuccessStatusCode, formBody);
                using var form = JsonDocument.Parse(formBody);
                formId = form.RootElement.GetProperty("id").GetString();
            }

            await OpenBpmnModelerAsync(page);
            await SelectTenantAsync(page, tenantName, tenantId!);
            await ImportBpmnAsync(page, CreateUserTaskBpmn(processKey, formKey));
            await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
            await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();

            await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
            await SelectTenantAsync(page, tenantName, tenantId!);
            await FillBoundInputAsync(page.GetByTestId("process-definition-search"), processKey);
            var definitionRow = page.Locator("tr").Filter(new() { HasText = processKey }).First;
            try
            {
                await definitionRow.WaitForAsync();
            }
            catch (TimeoutException exception)
            {
                var notifications = await page.Locator(".mud-snackbar").AllTextContentsAsync();
                throw new InvalidOperationException(
                    $"The deployed process '{processKey}' was not rendered after selecting tenant '{tenantId}'. " +
                    $"Page text: {await page.Locator("body").InnerTextAsync()}. " +
                    $"Notifications: {string.Join(" | ", notifications)}. " +
                    $"Recent API logs: {string.Join(" | ", host.ApiLogs.TakeLast(100))}. " +
                    $"Recent Studio logs: {string.Join(" | ", host.StudioLogs.TakeLast(100))}",
                    exception);
            }
            await definitionRow.Locator("button[aria-label='Start Process Instance']").ClickAsync();
            var startDialog = page.GetByRole(AriaRole.Dialog);
            await FillBoundInputAsync(startDialog.GetByLabel("Business Key", new() { Exact = true }), businessKey);
            await FillBoundInputAsync(startDialog.GetByLabel("Variable Name", new() { Exact = true }).First, "requestSource");
            await FillBoundInputAsync(startDialog.GetByLabel("Value", new() { Exact = true }).First, "Studio UI");
            await startDialog.GetByRole(AriaRole.Button, new() { Name = "Start Process", Exact = true }).ClickAsync();
            await page.GetByText("Process instance started successfully!", new() { Exact = true }).WaitForAsync();

            var instances = await apiClient.GetFromJsonAsync<JsonElement[]>($"api/runtime?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
            var started = Assert.Single(instances ?? [], candidate => candidate.GetProperty("processId").GetString() == processKey && candidate.GetProperty("businessKey").GetString() == businessKey);
            var instanceId = started.GetProperty("id").GetGuid();

            await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
            await SelectTenantAsync(page, tenantName, tenantId!);
            await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), businessKey);
            var instanceRow = page.Locator("tr").Filter(new() { HasText = businessKey }).First;
            await instanceRow.GetByText("1 task(s)", new() { Exact = true }).WaitForAsync();
            await instanceRow.GetByRole(AriaRole.Button, new() { Name = "View Details", Exact = true }).ClickAsync();
            var detailsDialog = page.GetByRole(AriaRole.Dialog);
            var variablesTab = detailsDialog.GetByRole(AriaRole.Tab, new() { Name = "Variables", Exact = true });
            try
            {
                await variablesTab.ClickAsync(new() { Timeout = 5_000 });
            }
            catch (TimeoutException exception)
            {
                var artifactDirectory = Environment.GetEnvironmentVariable("VERTEXBPMN_STUDIO_E2E_ARTIFACTS");
                if (!string.IsNullOrWhiteSpace(artifactDirectory))
                    await page.ScreenshotAsync(new() { Path = Path.Combine(artifactDirectory, "process-instance-tabs.png"), FullPage = true });
                var hitTarget = await variablesTab.EvaluateAsync<string>(
                    """
                    element => {
                      const bounds = element.getBoundingClientRect();
                      const hit = document.elementFromPoint(bounds.left + bounds.width / 2, bounds.top + bounds.height / 2);
                      return JSON.stringify({ bounds: { x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height }, hit: hit?.outerHTML });
                    }
                    """);
                throw new InvalidOperationException($"Variables tab is obscured. Hit-test: {hitTarget}", exception);
            }
            await detailsDialog.GetByText("requestSource", new() { Exact = true }).WaitForAsync();
            await detailsDialog.GetByText("Studio UI", new() { Exact = true }).WaitForAsync();
            await detailsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

            using var tasksResponse = await apiClient.GetAsync($"api/task?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
            var tasksBody = await tasksResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(
                tasksResponse.IsSuccessStatusCode,
                $"Task API returned {(int)tasksResponse.StatusCode}: {tasksBody}. Recent API logs: {string.Join(" | ", host.ApiLogs.TakeLast(100))}");
            var tasks = JsonSerializer.Deserialize<JsonElement[]>(tasksBody);
            var task = Assert.Single(tasks ?? [], candidate => candidate.GetProperty("processInstanceId").GetGuid() == instanceId);
            var taskId = task.GetProperty("id").GetGuid();
            Assert.Equal(formKey, task.GetProperty("formKey").GetString());
            Assert.Contains($"customer-{host.RunId}", task.GetProperty("formSchema").GetString(), StringComparison.Ordinal);

            await page.GotoAsync($"{host.StudioBaseAddress}tasks");
            await SelectTenantAsync(page, tenantName, tenantId!);
            await FillBoundInputAsync(page.GetByPlaceholder("Search"), instanceId.ToString());
            var taskRow = page.Locator("tr").Filter(new() { HasText = taskId.ToString() }).First;
            await taskRow.GetByRole(AriaRole.Button, new() { Name = "Claim Task", Exact = true }).ClickAsync();
            await page.GetByText($"Task {taskId} claimed!", new() { Exact = true }).WaitForAsync();
            taskRow = page.Locator("tr").Filter(new() { HasText = taskId.ToString() }).First;
            await taskRow.GetByRole(AriaRole.Button, new() { Name = "View Details", Exact = true }).ClickAsync();
            var taskDialog = page.GetByRole(AriaRole.Dialog);
            await taskDialog.GetByLabel("Customer number", new() { Exact = true }).FillAsync("ACME-42");
            await taskDialog.GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true }).ClickAsync();
            await page.GetByText("Task Approve request completed!", new() { Exact = true }).WaitForAsync();
            await page.Locator("tr").Filter(new() { HasText = taskId.ToString() }).WaitForAsync(new() { State = WaitForSelectorState.Detached });

            await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
            await SelectTenantAsync(page, tenantName, tenantId!);
            await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), businessKey);
            instanceRow = page.Locator("tr").Filter(new() { HasText = businessKey }).First;
            await instanceRow.GetByText("Completed", new() { Exact = true }).WaitForAsync();
            await instanceRow.GetByText("0 task(s)", new() { Exact = true }).WaitForAsync();
            await instanceRow.GetByRole(AriaRole.Button, new() { Name = "View Details", Exact = true }).ClickAsync();
            detailsDialog = page.GetByRole(AriaRole.Dialog);
            await detailsDialog.GetByRole(AriaRole.Tab, new() { Name = "Variables", Exact = true }).ClickAsync();
            await detailsDialog.GetByText($"customer-{host.RunId}", new() { Exact = true }).WaitForAsync();
            await detailsDialog.GetByText("ACME-42", new() { Exact = true }).WaitForAsync();
            await detailsDialog.GetByRole(AriaRole.Tab, new() { Name = "History", Exact = true }).ClickAsync();
            await detailsDialog.GetByText("User Task Completed", new() { Exact = true }).WaitForAsync();
            await detailsDialog.GetByText("Process Completed", new() { Exact = true }).WaitForAsync();
            await detailsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

            await page.GotoAsync($"{host.StudioBaseAddress}history");
            await SelectTenantAsync(page, tenantName, tenantId!);
            var instanceHistoryRows = page.Locator("tr").Filter(new() { HasText = instanceId.ToString() });
            await instanceHistoryRows.Filter(new() { HasText = "User Task Completed" }).First.WaitForAsync();
            await instanceHistoryRows.Filter(new() { HasText = "Process Completed" }).First.WaitForAsync();

            // Persistent event log (System B): verify via the API that the completed instance
            // produced the expected engine history events, independent of the browser tabs.
            using (var eventLogResponse = await apiClient.GetAsync(
                       $"api/history/by-process-instance/{instanceId}",
                       TestContext.Current.CancellationToken))
            {
                var eventLogBody = await eventLogResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.True(eventLogResponse.IsSuccessStatusCode, eventLogBody);
                var eventLog = JsonSerializer.Deserialize<JsonElement[]>(eventLogBody);
                Assert.NotNull(eventLog);
                Assert.NotEmpty(eventLog);
                Assert.All(
                    eventLog,
                    historyEvent => Assert.Equal(
                        instanceId,
                        historyEvent.GetProperty("processInstanceId").GetGuid()));
                var eventTypes = eventLog
                    .Select(historyEvent => historyEvent.GetProperty("eventType").GetString())
                    .ToHashSet(StringComparer.Ordinal);
                Assert.Contains("PROCESS_STARTED", eventTypes);
                Assert.Contains("USER_TASK_CREATED", eventTypes);
                Assert.Contains("USER_TASK_COMPLETED", eventTypes);
                Assert.Contains("PROCESS_COMPLETED", eventTypes);
            }

            await page.GotoAsync($"{host.StudioBaseAddress}event-log");
            await SelectTenantAsync(page, tenantName, tenantId!);
            var eventLogTable = page.GetByTestId("persistent-event-log-table");
            await eventLogTable.GetByText(instanceId.ToString(), new() { Exact = true }).First.WaitForAsync();
            await eventLogTable.GetByText("Process Completed", new() { Exact = true }).WaitForAsync();
            await eventLogTable.GetByText(taskId.ToString(), new() { Exact = false }).First.WaitForAsync();

            await page.GotoAsync($"{host.StudioBaseAddress}execution-details");
            await SelectTenantAsync(page, tenantName, tenantId!);
            await page.GetByRole(AriaRole.Button, new() { Name = "Load jobs", Exact = true }).ClickAsync();
            await page.GetByTestId("execution-details-result").GetByText("Jobs", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Load incidents", Exact = true }).ClickAsync();
            await page.GetByTestId("execution-details-result").GetByText("Incidents", new() { Exact = true }).WaitForAsync();
            await FillBoundInputAsync(
                page.GetByLabel("Process instance id for variables", new() { Exact = true }),
                instanceId.ToString());
            await page.GetByRole(AriaRole.Button, new() { Name = "Load variables", Exact = true }).ClickAsync();
            var variablesResult = page.GetByTestId("execution-details-result");
            await variablesResult.GetByText("Variables", new() { Exact = true }).WaitForAsync();
            await variablesResult.GetByText("requestSource", new() { Exact = false }).WaitForAsync();
            await variablesResult.GetByText($"customer-{host.RunId}", new() { Exact = false }).WaitForAsync();
            await variablesResult.GetByText("ACME-42", new() { Exact = false }).WaitForAsync();

            Assert.True(
                browserErrors.IsEmpty,
                $"Browser errors: {string.Join(" | ", browserErrors)}. Recent Studio logs: {string.Join(" | ", host.StudioLogs.TakeLast(150))}");
        }
        finally
        {
            await host.ClosePageAsync(page);
            if (!string.IsNullOrWhiteSpace(formId) && !string.IsNullOrWhiteSpace(tenantId))
                await apiClient.DeleteAsync($"api/forms/{Uri.EscapeDataString(formId)}?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ProcessManagement_DashboardAndDefinitions_RefreshPaginateVersionViewAndDeletePersistently()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var populatedTenantName = $"Management populated {host.RunId}";
        var emptyTenantName = $"Management empty {host.RunId}";
        var primaryProcessKey = $"Management_{host.RunId}";
        using var apiClient = host.CreateApiClient();
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);

        try
        {
            var populatedTenantId = await CreateTenantAsync(apiClient, populatedTenantName);
            var emptyTenantId = await CreateTenantAsync(apiClient, emptyTenantName);
            host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(populatedTenantId)}");
            host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(emptyTenantId)}");

            await DeployProcessAsync(apiClient, primaryProcessKey, populatedTenantId, "Management v1");
            await DeployProcessAsync(apiClient, primaryProcessKey, populatedTenantId, "Management v2");
            host.RegisterProcessDefinitionCleanup(primaryProcessKey, populatedTenantId);

            foreach (var index in Enumerable.Range(1, 10))
            {
                var processKey = $"{primaryProcessKey}_{index:00}";
                await DeployProcessAsync(apiClient, processKey, populatedTenantId, $"Management page item {index:00}");
                host.RegisterProcessDefinitionCleanup(processKey, populatedTenantId);
            }

            await page.GotoAsync(host.StudioBaseAddress.ToString());
            await SelectTenantAsync(page, populatedTenantName, populatedTenantId);
            await page.GetByTestId("dashboard-process-definitions-value").GetByText("12", new() { Exact = true }).WaitForAsync();

            await SelectTenantAsync(page, emptyTenantName, emptyTenantId);
            await page.GetByTestId("dashboard-process-definitions-value").GetByText("0", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Process Definitions", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).WaitForAsync();

            await SelectTenantAsync(page, populatedTenantName, populatedTenantId);
            var definitionsGrid = page.GetByTestId("process-definitions-grid");
            await definitionsGrid.Locator("tbody tr").First.WaitForAsync();
            Assert.Equal(10, await definitionsGrid.Locator("tbody tr").CountAsync());
            await definitionsGrid.Locator(".mud-table-pagination-actions button:not([disabled])").Last.ClickAsync();
            await definitionsGrid.GetByText($"{primaryProcessKey}_09", new() { Exact = true }).WaitForAsync();

            await FillBoundInputAsync(page.GetByTestId("process-definition-search"), primaryProcessKey);
            var definitionRow = definitionsGrid.Locator("tbody tr").Filter(new() { HasText = "Management v2" }).First;
            await definitionRow.GetByRole(AriaRole.Button, new() { Name = "View BPMN", Exact = true }).ClickAsync();
            var viewerDialog = page.GetByRole(AriaRole.Dialog);
            await viewerDialog.GetByTestId("bpmn-definition-xml").WaitForAsync();
            Assert.Contains(
                primaryProcessKey,
                await viewerDialog.GetByTestId("bpmn-definition-xml").GetByRole(AriaRole.Textbox).InputValueAsync(),
                StringComparison.Ordinal);
            await viewerDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

            await definitionRow.GetByRole(AriaRole.Button, new() { Name = "View Versions", Exact = true }).ClickAsync();
            var versionsDialog = page.GetByRole(AriaRole.Dialog);
            await versionsDialog.GetByText("v1", new() { Exact = false }).WaitForAsync();
            await versionsDialog.GetByText("v2", new() { Exact = false }).WaitForAsync();
            await versionsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

            await definitionRow.GetByRole(AriaRole.Button, new() { Name = "Delete Process Definition", Exact = true }).ClickAsync();
            var confirmation = page.GetByRole(AriaRole.Dialog);
            await confirmation.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await page.GetByText("Process definition 'Management v2' deleted.", new() { Exact = true }).WaitForAsync();
            await page.ReloadAsync();
            await FillBoundInputAsync(page.GetByTestId("process-definition-search"), primaryProcessKey);
            Assert.Equal(1, await definitionsGrid.Locator("tbody tr").Filter(new() { HasText = primaryProcessKey }).CountAsync());
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task ProcessManagement_InstancesAndTasks_SuspendResumeDeleteFilterAndCompleteWithoutVariables()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var tenantName = $"Management runtime {host.RunId}";
        var processKey = $"Management_Runtime_{host.RunId}";
        using var apiClient = host.CreateApiClient();
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);

        try
        {
            var tenantId = await CreateTenantAsync(apiClient, tenantName);
            host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
            host.RegisterProcessDefinitionCleanup(processKey, tenantId);
            await DeployProcessAsync(
                apiClient,
                processKey,
                tenantId,
                "Management runtime",
                CreateUserTaskBpmn(processKey, $"unused-form-{host.RunId}"));

            var lifecycleBusinessKey = $"lifecycle-{host.RunId}";
            var deletionBusinessKey = $"deletion-{host.RunId}";
            var lifecycleInstanceId = await StartProcessAsync(apiClient, processKey, tenantId, lifecycleBusinessKey);
            var deletionInstanceId = await StartProcessAsync(apiClient, processKey, tenantId, deletionBusinessKey);

            await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
            await SelectTenantAsync(page, tenantName, tenantId);
            await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), lifecycleBusinessKey);
            var lifecycleRow = page.Locator("tr").Filter(new() { HasText = lifecycleBusinessKey }).First;
            await lifecycleRow.GetByRole(AriaRole.Button, new() { Name = "Suspend Instance", Exact = true }).ClickAsync();
            await page.GetByText("Process instance suspended", new() { Exact = true }).WaitForAsync();
            lifecycleRow = page.Locator("tr").Filter(new() { HasText = lifecycleBusinessKey }).First;
            await lifecycleRow.GetByText("Suspended", new() { Exact = true }).WaitForAsync();
            await lifecycleRow.GetByRole(AriaRole.Button, new() { Name = "Resume Instance", Exact = true }).ClickAsync();
            await page.GetByText("Process instance resumed", new() { Exact = true }).WaitForAsync();
            lifecycleRow = page.Locator("tr").Filter(new() { HasText = lifecycleBusinessKey }).First;
            await lifecycleRow.Locator(".mud-chip-content").GetByText("Running", new() { Exact = true }).WaitForAsync();

            await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), deletionBusinessKey);
            var deletionRow = page.Locator("tr").Filter(new() { HasText = deletionBusinessKey }).First;
            await deletionRow.GetByRole(AriaRole.Button, new() { Name = "Delete Instance", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await page.GetByText("Process instance deleted", new() { Exact = true }).WaitForAsync();
            await page.ReloadAsync();
            await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), deletionBusinessKey);
            Assert.Equal(0, await page.Locator("tr").Filter(new() { HasText = deletionInstanceId.ToString() }).CountAsync());

            using var tasksResponse = await apiClient.GetAsync(
                $"api/task?tenantId={Uri.EscapeDataString(tenantId)}",
                TestContext.Current.CancellationToken);
            var tasksBody = await tasksResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(tasksResponse.IsSuccessStatusCode, tasksBody);
            var tasks = JsonSerializer.Deserialize<JsonElement[]>(tasksBody);
            var lifecycleTask = Assert.Single(
                tasks ?? [],
                candidate => candidate.GetProperty("processInstanceId").GetGuid() == lifecycleInstanceId);
            var taskId = lifecycleTask.GetProperty("id").GetGuid();

            await page.GotoAsync($"{host.StudioBaseAddress}tasks");
            await SelectTenantAsync(page, tenantName, tenantId);
            await FillBoundInputAsync(page.GetByPlaceholder("Search"), lifecycleInstanceId.ToString());
            var taskRow = page.Locator("tr").Filter(new() { HasText = taskId.ToString() }).First;
            await taskRow.GetByRole(AriaRole.Button, new() { Name = "Claim Task", Exact = true }).ClickAsync();
            await page.GetByText($"Task {taskId} claimed!", new() { Exact = true }).WaitForAsync();
            taskRow = page.Locator("tr").Filter(new() { HasText = taskId.ToString() }).First;
            await taskRow.GetByText("UI Test User", new() { Exact = true }).WaitForAsync();
            await taskRow.GetByRole(AriaRole.Button, new() { Name = "Complete Task", Exact = true }).ClickAsync();
            await page.GetByText($"Task {taskId} completed!", new() { Exact = true }).WaitForAsync();
            await taskRow.WaitForAsync(new() { State = WaitForSelectorState.Detached });
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task ProcessManagement_Deployments_ValidateSizeUploadMultipleAndAppearInDefinitions()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var tenantName = $"Management deployments {host.RunId}";
        var processKeys = new[]
        {
            $"Management_Deployment_{host.RunId}_Single",
            $"Management_Deployment_{host.RunId}_MultipleA",
            $"Management_Deployment_{host.RunId}_MultipleB"
        };
        using var apiClient = host.CreateApiClient();
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);

        try
        {
            var tenantId = await CreateTenantAsync(apiClient, tenantName);
            host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
            foreach (var processKey in processKeys)
                host.RegisterProcessDefinitionCleanup(processKey, tenantId);

            await page.GotoAsync($"{host.StudioBaseAddress}deployments");
            await SelectTenantAsync(page, tenantName, tenantId);
            var upload = page.GetByTestId("deployment-upload");
            await upload.SetInputFilesAsync(new FilePayload
            {
                Name = "management-single.bpmn",
                MimeType = "application/xml",
                Buffer = Encoding.UTF8.GetBytes(CreateBpmn(processKeys[0]))
            });
            await page.GetByText("File management-single.bpmn deployed successfully!", new() { Exact = true }).WaitForAsync();
            await page.GetByTestId("deployments-table").GetByText("management-single.bpmn", new() { Exact = true }).WaitForAsync();

            await upload.SetInputFilesAsync(new FilePayload
            {
                Name = "invalid.bpmn",
                MimeType = "application/xml",
                Buffer = Encoding.UTF8.GetBytes("<definitions><process></definitions>")
            });
            await page.GetByText("invalid.bpmn is not valid BPMN XML.", new() { Exact = true }).WaitForAsync();

            await upload.SetInputFilesAsync(new FilePayload
            {
                Name = "too-large.bpmn",
                MimeType = "application/xml",
                Buffer = new byte[(10 * 1024 * 1024) + 1]
            });
            await page.GetByText("too-large.bpmn exceeds the 10 MB upload limit.", new() { Exact = true }).WaitForAsync();

            await upload.SetInputFilesAsync(
            [
                new FilePayload
                {
                    Name = "management-multiple-a.bpmn",
                    MimeType = "application/xml",
                    Buffer = Encoding.UTF8.GetBytes(CreateBpmn(processKeys[1]))
                },
                new FilePayload
                {
                    Name = "management-multiple-b.bpmn",
                    MimeType = "application/xml",
                    Buffer = Encoding.UTF8.GetBytes(CreateBpmn(processKeys[2]))
                }
            ]);
            await page.GetByText("2 BPMN files deployed successfully!", new() { Exact = true }).WaitForAsync();
            await page.GetByTestId("deployments-table").GetByText("management-multiple-a.bpmn", new() { Exact = true }).WaitForAsync();
            await page.GetByTestId("deployments-table").GetByText("management-multiple-b.bpmn", new() { Exact = true }).WaitForAsync();

            await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
            await SelectTenantAsync(page, tenantName, tenantId);
            foreach (var processKey in processKeys)
            {
                await FillBoundInputAsync(page.GetByTestId("process-definition-search"), processKey);
                await page.GetByTestId("process-definitions-grid").GetByText(processKey, new() { Exact = true }).WaitForAsync();
            }
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task N8nImporter_ReportsNeedsReviewValidatesDeploysReloadsAndExports_ARealWorkflow()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var workflowName = $"StudioE2EN8n_{host.RunId}";
        var processKey = $"n8n_{workflowName}";
        host.RegisterProcessDefinitionCleanup(processKey);
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                browserErrors.Enqueue($"console: {message.Text}");
        };

        try
        {
            await OpenBpmnModelerAsync(page);
            await page.GetByTestId("n8n-import-file").SetInputFilesAsync(new FilePayload
            {
                Name = "real-workflow.json",
                MimeType = "application/json",
                Buffer = Encoding.UTF8.GetBytes(CreateN8nWorkflow(workflowName))
            });
            await WaitForTextWithDiagnosticsAsync(
                page,
                "Imported n8n workflow real-workflow.json.",
                browserErrors,
                "n8n import");

            var report = page.GetByTestId("n8n-import-report");
            await report.GetByText("Webhook", new() { Exact = false }).First.WaitForAsync();
            await report.GetByText("Request", new() { Exact = false }).First.WaitForAsync();
            await report.GetByText("No credential reference was imported.", new() { Exact = false }).WaitForAsync();

            var importedXml = await WaitForPreviewXmlAsync(page, processKey);
            Assert.Contains("serviceTask", importedXml, StringComparison.Ordinal);
            Assert.Contains("connector=\"http\"", importedXml, StringComparison.Ordinal);
            Assert.DoesNotContain("credentialRef", importedXml, StringComparison.Ordinal);

            await page.GetByRole(AriaRole.Button, new() { Name = "Validate", Exact = true }).ClickAsync();
            await page.GetByText("No issues", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(
                page,
                "BPMN deployed successfully.",
                browserErrors,
                "n8n-generated BPMN deployment");

            using var apiClient = host.CreateApiClient();
            var definitions = await apiClient.GetFromJsonAsync<JsonElement[]>(
                $"api/repository?key={Uri.EscapeDataString(processKey)}",
                TestContext.Current.CancellationToken);
            Assert.Single(definitions ?? []);

            await OpenBpmnModelerAsync(page);
            await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Option).Filter(new() { HasText = processKey }).First.ClickAsync();
            await page.GetByTestId("bpmn-version-load-action").ClickAsync();
            await page.GetByText("Deployed BPMN version loaded into the editor.", new() { Exact = true }).WaitForAsync();
            await WaitForPreviewXmlAsync(page, processKey);

            var download = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export XML", Exact = true }).ClickAsync());
            var exportedXml = await File.ReadAllTextAsync(await download.PathAsync(), TestContext.Current.CancellationToken);
            Assert.Contains(
                XDocument.Parse(exportedXml).Descendants(),
                element => element.Name.LocalName == "process"
                           && (string?)element.Attribute("id") == processKey);
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task DmnModeler_ImportsDeploysReloadsEvaluatesAndExports_ARealDecision()
    {
        Assert.SkipUnless(
            LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var decisionKey = $"studio-e2e-decision-{host.RunId}";
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                browserErrors.Enqueue($"console: {message.Text}");
        };

        try
        {
            await page.GotoAsync($"{host.StudioBaseAddress}dmn-modeler");
            await page.GetByRole(AriaRole.Heading, new() { Name = "DMN Modeler", Exact = true }).WaitForAsync();
            await page.GetByTestId("dmn-modeler-shell").WaitForAsync();
            await page.Locator("[data-modeler-ready='true']").First.WaitForAsync();
            await FillBoundInputAsync(page.GetByLabel("Decision key", new() { Exact = true }), decisionKey);
            await FillBoundInputAsync(page.GetByLabel("Name", new() { Exact = true }), "Local Studio E2E decision");
            await page.GetByTestId("dmn-import-file").SetInputFilesAsync(new FilePayload
            {
                Name = "real-decision.dmn",
                MimeType = "application/xml",
                Buffer = Encoding.UTF8.GetBytes(CreateDmn(decisionKey))
            });
            await WaitForTextWithDiagnosticsAsync(
                page,
                "Imported real-decision.dmn.",
                browserErrors,
                "DMN import");

            await page.GetByRole(AriaRole.Button, new() { Name = "Add decision rule", Exact = true }).ClickAsync();
            await page.GetByText("Decision rule added.", new() { Exact = true }).WaitForAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Deploy DMN", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(
                page,
                $"DMN '{decisionKey}' deployed successfully.",
                browserErrors,
                "DMN deployment");

            using var apiClient = host.CreateApiClient();
            using var definitionResponse = await apiClient.GetAsync(
                $"api/decision/by-key?decisionKey={Uri.EscapeDataString(decisionKey)}",
                TestContext.Current.CancellationToken);
            var definitionBody = await definitionResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(definitionResponse.IsSuccessStatusCode, definitionBody);
            using (var definition = JsonDocument.Parse(definitionBody))
            {
                Assert.Equal(decisionKey, definition.RootElement.GetProperty("key").GetString());
                Assert.Equal("Local Studio E2E decision", definition.RootElement.GetProperty("name").GetString());
            }

            await page.GotoAsync($"{host.StudioBaseAddress}dmn-modeler");
            await page.Locator("[data-modeler-ready='true']").First.WaitForAsync();
            var decisionKeyInput = page.GetByLabel("Decision key", new() { Exact = true });
            await FillBoundInputAsync(decisionKeyInput, decisionKey);
            await page.GetByRole(AriaRole.Button, new() { Name = "Load from API", Exact = true }).ClickAsync();
            var reloadNotification = page.Locator(".mud-snackbar").Last;
            await reloadNotification.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            Assert.Contains("DMN loaded from API.", await reloadNotification.InnerTextAsync(), StringComparison.Ordinal);
            var download = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export DMN", Exact = true }).ClickAsync());
            var downloadedXml = await File.ReadAllTextAsync(
                await download.PathAsync(),
                TestContext.Current.CancellationToken);
            Assert.Contains(
                XDocument.Parse(downloadedXml).Descendants(),
                element => element.Name.LocalName == "decision"
                           && (string?)element.Attribute("id") == decisionKey);
            Assert.Equal(3, XDocument.Parse(downloadedXml).Descendants().Count(element => element.Name.LocalName == "rule"));
            await page.GetByTestId("dmn-import-file").SetInputFilesAsync(new FilePayload
            {
                Name = "exported-decision.dmn",
                MimeType = "application/xml",
                Buffer = Encoding.UTF8.GetBytes(downloadedXml)
            });
            await WaitForTextWithDiagnosticsAsync(
                page,
                "Imported exported-decision.dmn.",
                browserErrors,
                "DMN export roundtrip");

            var inputName = page.GetByLabel("Input name", new() { Exact = true });
            await FillBoundInputAsync(inputName, "amount");
            var inputValue = page.GetByLabel("Input value", new() { Exact = true });
            await FillBoundInputAsync(inputValue, "150");
            await page.GetByRole(AriaRole.Button, new() { Name = "Evaluate", Exact = true }).ClickAsync();
            await page.GetByText("Decision evaluated successfully.", new() { Exact = true }).WaitForAsync();
            await page.GetByText("result: high", new() { Exact = true }).WaitForAsync();

            await FillBoundInputAsync(inputValue, "50");
            await page.GetByRole(AriaRole.Button, new() { Name = "Evaluate", Exact = true }).ClickAsync();
            await page.GetByText("result: low", new() { Exact = true }).WaitForAsync();

            await FillBoundInputAsync(inputValue, "-1");
            await page.GetByRole(AriaRole.Button, new() { Name = "Evaluate", Exact = true }).ClickAsync();
            await page.GetByText("result: low", new() { Exact = true }).WaitForAsync(
                new() { State = WaitForSelectorState.Detached });
            Assert.Equal(0, await page.GetByText("result:", new() { Exact = false }).CountAsync());
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    [Fact]
    public async Task FormBuilder_ImportsSavesReloadsAndExports_ARealTenantForm()
    {
        Assert.SkipUnless(
            LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var tenantName = $"Studio E2E {host.RunId}";
        var formKey = $"studio-e2e-form-{host.RunId}";
        string? tenantId = null;
        string? formId = null;
        using var apiClient = host.CreateApiClient();
        var page = await host.CreatePageAsync();
        var browserErrors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                browserErrors.Enqueue($"console: {message.Text}");
        };

        try
        {
            using (var tenantResponse = await apiClient.PostAsJsonAsync(
                       "api/tenant",
                       new { name = tenantName, description = "Local Studio browser test" },
                       TestContext.Current.CancellationToken))
            {
                var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
                tenantId = JsonDocument.Parse(tenantBody).RootElement.GetProperty("id").GetString();
                Assert.False(string.IsNullOrWhiteSpace(tenantId));
                host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
            }

            await page.GotoAsync($"{host.StudioBaseAddress}form-builder");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Form Builder", Exact = true }).WaitForAsync();
            await page.Locator("[data-modeler-ready='true']").First.WaitForAsync();
            await SelectTenantAsync(page, tenantName, tenantId!);
            await FillBoundInputAsync(page.GetByLabel("Form key", new() { Exact = true }), formKey);
            await FillBoundInputAsync(page.GetByLabel("Form name", new() { Exact = true }), "Local Studio E2E form");
            await page.GetByTestId("form-import-file").SetInputFilesAsync(new FilePayload
            {
                Name = "real-form.json",
                MimeType = "application/json",
                Buffer = Encoding.UTF8.GetBytes(CreateFormJson(host.RunId))
            });
            await page.GetByText("Imported real-form.json.", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Save form", Exact = true }).ClickAsync();
            await page.GetByText(
                "Form saved in the registry and refreshed in the Runtime Viewer.",
                new() { Exact = true }).WaitForAsync();

            var forms = await apiClient.GetFromJsonAsync<JsonElement[]>(
                $"api/forms?tenantId={Uri.EscapeDataString(tenantId!)}",
                TestContext.Current.CancellationToken);
            var persisted = Assert.Single(
                forms ?? [],
                form => form.GetProperty("key").GetString() == formKey);
            formId = persisted.GetProperty("id").GetString();
            Assert.Equal("Local Studio E2E form", persisted.GetProperty("name").GetString());
            Assert.Contains($"customer-{host.RunId}", persisted.GetProperty("schema").GetString(), StringComparison.Ordinal);

            await page.GotoAsync($"{host.StudioBaseAddress}form-builder");
            await page.Locator("[data-modeler-ready='true']").First.WaitForAsync();
            await SelectTenantAsync(page, tenantName, tenantId!);
            await FillBoundInputAsync(page.GetByLabel("Form key", new() { Exact = true }), formKey);
            await page.GetByRole(AriaRole.Button, new() { Name = "Load saved form", Exact = true }).ClickAsync();
            await page.GetByText("Saved form loaded from the registry.", new() { Exact = true }).WaitForAsync();
            await page.GetByLabel("Customer number", new() { Exact = true }).Last.WaitForAsync();

            await FillBoundInputAsync(page.GetByLabel("New field key", new() { Exact = true }), $"approval-{host.RunId}");
            await FillBoundInputAsync(page.GetByLabel("New field label", new() { Exact = true }), "Approval note");
            await page.GetByRole(AriaRole.Button, new() { Name = "Add text field", Exact = true }).ClickAsync();
            await page.GetByText("Text field added to the form.", new() { Exact = true }).WaitForAsync();
            await FillBoundInputAsync(page.GetByLabel("Form name", new() { Exact = true }), "Local Studio E2E form updated");
            await page.GetByRole(AriaRole.Button, new() { Name = "Save form", Exact = true }).ClickAsync();
            await page.GetByText(
                "Form saved in the registry and refreshed in the Runtime Viewer.",
                new() { Exact = true }).Last.WaitForAsync();

            forms = await apiClient.GetFromJsonAsync<JsonElement[]>(
                $"api/forms?tenantId={Uri.EscapeDataString(tenantId!)}",
                TestContext.Current.CancellationToken);
            persisted = Assert.Single(forms ?? [], form => form.GetProperty("key").GetString() == formKey);
            Assert.Equal(formId, persisted.GetProperty("id").GetString());
            Assert.Equal("Local Studio E2E form updated", persisted.GetProperty("name").GetString());

            var download = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export JSON", Exact = true }).ClickAsync());
            var downloadedJson = await File.ReadAllTextAsync(
                await download.PathAsync(),
                TestContext.Current.CancellationToken);
            using var exportedForm = JsonDocument.Parse(downloadedJson);
            Assert.Contains(
                exportedForm.RootElement.GetProperty("components").EnumerateArray(),
                component => component.GetProperty("key").GetString() == $"customer-{host.RunId}");
            Assert.Contains(
                exportedForm.RootElement.GetProperty("components").EnumerateArray(),
                component => component.GetProperty("key").GetString() == $"approval-{host.RunId}"
                             && component.GetProperty("label").GetString() == "Approval note");

            await page.GetByTestId("form-import-file").SetInputFilesAsync(new FilePayload
            {
                Name = "exported-form.json",
                MimeType = "application/json",
                Buffer = Encoding.UTF8.GetBytes(downloadedJson)
            });
            await page.GetByText("Imported exported-form.json.", new() { Exact = true }).WaitForAsync();
            await page.GetByLabel("Customer number", new() { Exact = true }).Last.WaitForAsync();
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
            if (!string.IsNullOrWhiteSpace(formId) && !string.IsNullOrWhiteSpace(tenantId))
                await apiClient.DeleteAsync(
                    $"api/forms/{Uri.EscapeDataString(formId)}?tenantId={Uri.EscapeDataString(tenantId)}",
                    TestContext.Current.CancellationToken);
            if (!string.IsNullOrWhiteSpace(tenantId))
                await apiClient.DeleteAsync(
                    $"api/tenant/{Uri.EscapeDataString(tenantId)}",
                    TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CmmnModeler_ImportsRegistersExecutesUpdatesAndExports_ARealCase()
    {
        Assert.SkipUnless(
            LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var caseKey = $"studio-e2e-case-{host.RunId}";
        var reviewId = $"review-{host.RunId}";
        var milestoneId = $"opened-{host.RunId}";
        var eventId = $"approve-{host.RunId}";
        var caseFileItemId = $"customer-{host.RunId}";
        var browserErrors = new ConcurrentQueue<string>();
        var page = await host.CreatePageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                browserErrors.Enqueue($"console: {message.Text}");
        };

        try
        {
            await page.GotoAsync($"{host.StudioBaseAddress}cmmn-modeler");
            await page.GetByRole(AriaRole.Heading, new() { Name = "CMMN Modeler", Exact = true }).WaitForAsync();
            await page.GetByTestId("cmmn-modeler-shell").WaitForAsync();
            await page.Locator("[data-modeler-ready='true']").First.WaitForAsync();
            await FillBoundInputAsync(page.GetByLabel("Case ID", new() { Exact = true }), caseKey);
            await page.GetByTestId("cmmn-import-file").SetInputFilesAsync(new FilePayload
            {
                Name = "real-case.cmmn",
                MimeType = "application/xml",
                Buffer = Encoding.UTF8.GetBytes(CreateCmmn(caseKey, reviewId, milestoneId, eventId, caseFileItemId))
            });
            await WaitForTextWithDiagnosticsAsync(page, "Imported real-case.cmmn.", browserErrors, "CMMN import");

            var capturedDownload = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export CMMN", Exact = true }).ClickAsync());
            var capturedXml = await File.ReadAllTextAsync(
                await capturedDownload.PathAsync(),
                TestContext.Current.CancellationToken);
            var capturedDiscretionaryItem = XDocument.Parse(capturedXml).Descendants()
                .Single(element => element.Name.LocalName == "discretionaryItem");
            Assert.True(
                !string.IsNullOrWhiteSpace((string?)capturedDiscretionaryItem.Attribute("definitionRef")),
                capturedXml);

            await page.GetByRole(AriaRole.Button, new() { Name = "Register case model", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(
                page,
                $"CMMN model registered for case '{caseKey}'.",
                browserErrors,
                "CMMN registration");

            using var apiClient = host.CreateApiClient();
            using var definitionResponse = await apiClient.GetAsync(
                $"api/case-definitions/{Uri.EscapeDataString(caseKey)}",
                TestContext.Current.CancellationToken);
            var definitionBody = await definitionResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(definitionResponse.IsSuccessStatusCode, definitionBody);
            using (var definition = JsonDocument.Parse(definitionBody))
            {
                Assert.Equal(caseKey, definition.RootElement.GetProperty("key").GetString());
                Assert.Contains(reviewId, definition.RootElement.GetProperty("cmmnXml").GetString(), StringComparison.Ordinal);
            }

            await page.GetByRole(AriaRole.Button, new() { Name = "Execute case", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(
                page,
                $"Case '{caseKey}' executed.",
                browserErrors,
                "CMMN execution");
            await page.GetByText($"PLAN_ITEM_ACTIVE:{reviewId}:humantask", new() { Exact = true }).WaitForAsync();
            await page.GetByText($"PLAN_ITEM_COMPLETED:{milestoneId}:milestone", new() { Exact = true }).First.WaitForAsync();
            await page.GetByText("Completed: 1", new() { Exact = true }).WaitForAsync();

            await FillBoundInputAsync(page.GetByLabel("Case-file item id", new() { Exact = true }), caseFileItemId);
            await FillBoundInputAsync(page.GetByLabel("Value", new() { Exact = true }), "ACME-42");
            await page.GetByRole(AriaRole.Button, new() { Name = "Update case file", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(
                page,
                $"Case-file item '{caseFileItemId}' updated.",
                browserErrors,
                "CMMN case-file update");

            await FillBoundInputAsync(page.GetByLabel("User event id", new() { Exact = true }), eventId);
            await page.GetByRole(AriaRole.Button, new() { Name = "Trigger user event", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(
                page,
                $"User event '{eventId}' triggered.",
                browserErrors,
                "CMMN user event");

            await page.GetByRole(AriaRole.Button, new() { Name = "Generate ad-hoc subprocess", Exact = true }).ClickAsync();
            await WaitForTextWithDiagnosticsAsync(
                page,
                "Ad-hoc subprocess generated.",
                browserErrors,
                "CMMN discretionary-item activation");

            await page.GetByRole(AriaRole.Button, new() { Name = "Load history", Exact = true }).ClickAsync();
            await page.GetByText("Loaded 4 historical snapshot(s).", new() { Exact = true }).WaitForAsync();
            await page.GetByText(
                $"Case-file values: {caseFileItemId}=ACME-42",
                new() { Exact = true }).Last.WaitForAsync();

            await FillBoundInputAsync(page.GetByLabel("New task name", new() { Exact = true }), "Escalation review");
            await page.GetByRole(AriaRole.Button, new() { Name = "Add human task", Exact = true }).ClickAsync();
            await page.GetByText("Human task added to the case model.", new() { Exact = true }).WaitForAsync();
            var download = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export CMMN", Exact = true }).ClickAsync());
            var downloadedXml = await File.ReadAllTextAsync(
                await download.PathAsync(),
                TestContext.Current.CancellationToken);
            Assert.Contains(
                XDocument.Parse(downloadedXml).Descendants(),
                element => element.Name.LocalName == "case"
                           && (string?)element.Attribute("id") == caseKey);
            Assert.Contains(
                XDocument.Parse(downloadedXml).Descendants(),
                element => element.Name.LocalName == "humanTask"
                           && (string?)element.Attribute("name") == "Escalation review");
            await page.GetByTestId("cmmn-import-file").SetInputFilesAsync(new FilePayload
            {
                Name = "exported-case.cmmn",
                MimeType = "application/xml",
                Buffer = Encoding.UTF8.GetBytes(downloadedXml)
            });
            await WaitForTextWithDiagnosticsAsync(
                page,
                "Imported exported-case.cmmn.",
                browserErrors,
                "CMMN export roundtrip");
            var roundtripDownload = await page.RunAndWaitForDownloadAsync(() =>
                page.GetByRole(AriaRole.Button, new() { Name = "Export CMMN", Exact = true }).ClickAsync());
            var roundtripXml = await File.ReadAllTextAsync(
                await roundtripDownload.PathAsync(),
                TestContext.Current.CancellationToken);
            Assert.Equal(
                caseKey,
                (string?)XDocument.Parse(roundtripXml).Descendants()
                    .Single(element => element.Name.LocalName == "case")
                    .Attribute("id"));
            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
        }
    }

    private async Task OpenBpmnModelerAsync(IPage page)
    {
        var response = await page.GotoAsync($"{host.StudioBaseAddress}bpmn-modeler");
        Assert.NotNull(response);
        Assert.True(response.Ok, $"Studio returned HTTP {response.Status}.");
        var shell = page.GetByTestId("bpmn-modeler-shell");
        await shell.WaitForAsync();
        for (var attempt = 0; attempt < 80; attempt++)
        {
            if (await page.Locator("[data-modeler-ready='true'] .djs-container").CountAsync() > 0)
                return;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException(
            $"The real BPMN modeler did not initialize. API logs: {string.Join(" | ", host.ApiLogs)}. " +
            $"Studio logs: {string.Join(" | ", host.StudioLogs)}. Shell: {await shell.InnerHTMLAsync()}");
    }

    private static async Task<string> CreateTenantAsync(HttpClient apiClient, string tenantName)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "api/tenant",
            new { name = tenantName, description = "Phase 3 process-management E2E" },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        using var tenant = JsonDocument.Parse(body);
        var tenantId = tenant.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(tenantId));
        return tenantId;
    }

    private static async Task DeployProcessAsync(
        HttpClient apiClient,
        string processKey,
        string tenantId,
        string name,
        string? bpmnXml = null)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "api/repository",
            new { bpmnXml = bpmnXml ?? CreateBpmn(processKey), name, tenantId },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static async Task<Guid> StartProcessAsync(
        HttpClient apiClient,
        string processKey,
        string tenantId,
        string businessKey)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "api/runtime/start",
            new { processDefinitionKey = processKey, variables = new Dictionary<string, object>(), businessKey, tenantId },
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        using var instance = JsonDocument.Parse(body);
        return instance.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task ImportBpmnAsync(IPage page, string xml)
    {
        await page.GetByTestId("bpmn-import-file").SetInputFilesAsync(new FilePayload
        {
            Name = "real-roundtrip.bpmn",
            MimeType = "application/xml",
            Buffer = Encoding.UTF8.GetBytes(xml)
        });
        await page.GetByText("Imported real-roundtrip.bpmn.", new() { Exact = true }).WaitForAsync();
    }

    private static async Task<string> WaitForPreviewXmlAsync(IPage page, string expectedToken)
    {
        var preview = page.GetByTestId("bpmn-xml-preview").GetByRole(AriaRole.Textbox);
        var lastXml = string.Empty;
        for (var attempt = 0; attempt < 120; attempt++)
        {
            lastXml = await preview.InputValueAsync();
            if (lastXml.Contains(expectedToken, StringComparison.Ordinal))
                return lastXml;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"The BPMN XML preview did not contain '{expectedToken}'. Last XML: {lastXml}");
    }

    private static async Task<string> WaitForPreviewXmlWithoutAsync(
        IPage page,
        string expectedToken,
        string forbiddenToken)
    {
        var preview = page.GetByTestId("bpmn-xml-preview").GetByRole(AriaRole.Textbox);
        var lastXml = string.Empty;
        for (var attempt = 0; attempt < 120; attempt++)
        {
            lastXml = await preview.InputValueAsync();
            if (lastXml.Contains(expectedToken, StringComparison.Ordinal)
                && !lastXml.Contains(forbiddenToken, StringComparison.Ordinal))
                return lastXml;
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException(
            $"The BPMN XML preview did not converge to a document containing '{expectedToken}' without '{forbiddenToken}'. Last XML: {lastXml}");
    }

    private async Task WaitForTextWithDiagnosticsAsync(
        IPage page,
        string text,
        ConcurrentQueue<string> browserErrors,
        string operation)
    {
        try
        {
            await page.GetByText(text, new() { Exact = true }).WaitForAsync(new() { Timeout = 10_000 });
        }
        catch (TimeoutException)
        {
            var notifications = await page.Locator(".mud-snackbar").AllTextContentsAsync();
            var recentBrowserErrors = browserErrors.TakeLast(50);
            var recentApiLogs = host.ApiLogs.TakeLast(100);
            var recentStudioLogs = host.StudioLogs.TakeLast(100);
            throw new InvalidOperationException(
                $"{operation} did not produce '{text}'. Browser errors: {string.Join(" | ", recentBrowserErrors)}. " +
                $"Notifications: {string.Join(" | ", notifications)}. " +
                $"Recent API logs: {string.Join(" | ", recentApiLogs)}. " +
                $"Recent Studio logs: {string.Join(" | ", recentStudioLogs)}");
        }
    }

    private static async Task FillBoundInputAsync(ILocator input, string value)
    {
        await input.ClickAsync();
        await input.PressAsync("ControlOrMeta+A");
        await input.PressSequentiallyAsync(value, new() { Delay = 5 });
        await input.BlurAsync();
        await Task.Delay(500, TestContext.Current.CancellationToken);
    }

    private async Task SelectTenantAsync(IPage page, string tenantName, string tenantId)
    {
        var tenantSelector = page.GetByRole(AriaRole.Combobox, new() { Name = "Tenant", Exact = true });
        for (var loadAttempt = 0; loadAttempt < 2 && !await tenantSelector.IsEnabledAsync(); loadAttempt++)
        {
            for (var attempt = 0; attempt < 240 && !await tenantSelector.IsEnabledAsync(); attempt++)
                await Task.Delay(250, TestContext.Current.CancellationToken);

            if (!await tenantSelector.IsEnabledAsync() && loadAttempt == 0)
                await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        Assert.True(await tenantSelector.IsEnabledAsync(), "Tenant selector did not finish loading.");
        var option = page.GetByRole(AriaRole.Option, new() { Name = tenantName, Exact = true });
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await page.Keyboard.PressAsync("Escape");
            await tenantSelector.ClickAsync();
            try
            {
                await option.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 1_000 });
                await option.ClickAsync();
                await option.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
                return;
            }
            catch (TimeoutException)
            {
                // A concurrent Blazor render can close the popover; reopen it on the next attempt.
            }

            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        var options = await page.GetByRole(AriaRole.Option).AllTextContentsAsync();
        throw new TimeoutException(
            $"Tenant selector did not switch to '{tenantName}' ({tenantId}). " +
            $"Available options: {string.Join(" | ", options)}. " +
            $"Recent API logs: {string.Join(" | ", host.ApiLogs.TakeLast(100))}. " +
            $"Recent Studio logs: {string.Join(" | ", host.StudioLogs.TakeLast(100))}");
    }

    private static string CreateBpmn(string processKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_{{processKey}}" targetNamespace="https://vertexbpmn.io/local-e2e">
          <bpmn:process id="{{processKey}}" name="Local Studio E2E {{processKey}}" isExecutable="true">
            <bpmn:startEvent id="Start_{{processKey}}" />
            <bpmn:sequenceFlow id="Flow_{{processKey}}" sourceRef="Start_{{processKey}}" targetRef="End_{{processKey}}" />
            <bpmn:endEvent id="End_{{processKey}}" />
          </bpmn:process>
          <bpmndi:BPMNDiagram id="Diagram_{{processKey}}">
            <bpmndi:BPMNPlane id="Plane_{{processKey}}" bpmnElement="{{processKey}}">
              <bpmndi:BPMNShape id="Start_{{processKey}}_di" bpmnElement="Start_{{processKey}}"><dc:Bounds x="180" y="120" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="End_{{processKey}}_di" bpmnElement="End_{{processKey}}"><dc:Bounds x="380" y="120" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNEdge id="Flow_{{processKey}}_di" bpmnElement="Flow_{{processKey}}"><di:waypoint x="216" y="138" /><di:waypoint x="380" y="138" /></bpmndi:BPMNEdge>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn:definitions>
        """;

    private static string CreateUserTaskBpmn(string processKey, string formKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0" id="Definitions_{{processKey}}" targetNamespace="https://vertexbpmn.io/local-e2e">
          <bpmn:process id="{{processKey}}" name="Local Studio task E2E" isExecutable="true">
            <bpmn:startEvent id="Start_{{processKey}}" />
            <bpmn:sequenceFlow id="Flow_Start_{{processKey}}" sourceRef="Start_{{processKey}}" targetRef="Approve_{{processKey}}" />
            <bpmn:userTask id="Approve_{{processKey}}" name="Approve request">
              <bpmn:extensionElements><vertex:form formRef="{{formKey}}" formVersion="1" /></bpmn:extensionElements>
            </bpmn:userTask>
            <bpmn:sequenceFlow id="Flow_End_{{processKey}}" sourceRef="Approve_{{processKey}}" targetRef="End_{{processKey}}" />
            <bpmn:endEvent id="End_{{processKey}}" />
          </bpmn:process>
          <bpmndi:BPMNDiagram id="Diagram_{{processKey}}">
            <bpmndi:BPMNPlane id="Plane_{{processKey}}" bpmnElement="{{processKey}}">
              <bpmndi:BPMNShape id="Start_{{processKey}}_di" bpmnElement="Start_{{processKey}}"><dc:Bounds x="120" y="120" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Approve_{{processKey}}_di" bpmnElement="Approve_{{processKey}}"><dc:Bounds x="230" y="98" width="100" height="80" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="End_{{processKey}}_di" bpmnElement="End_{{processKey}}"><dc:Bounds x="410" y="120" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNEdge id="Flow_Start_{{processKey}}_di" bpmnElement="Flow_Start_{{processKey}}"><di:waypoint x="156" y="138" /><di:waypoint x="230" y="138" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_End_{{processKey}}_di" bpmnElement="Flow_End_{{processKey}}"><di:waypoint x="330" y="138" /><di:waypoint x="410" y="138" /></bpmndi:BPMNEdge>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn:definitions>
        """;

    private static string CreateDmn(string decisionKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/" xmlns:dmndi="https://www.omg.org/spec/DMN/20191111/DMNDI/" xmlns:dc="http://www.omg.org/spec/DMN/20180521/DC/" id="definitions_{{decisionKey}}" name="Local Studio E2E" namespace="https://vertexbpmn.io/local-e2e">
          <decision id="{{decisionKey}}" name="Risk classification">
            <decisionTable id="table_{{decisionKey}}" hitPolicy="UNIQUE">
              <input id="input_{{decisionKey}}">
                <inputExpression id="expression_{{decisionKey}}" typeRef="number"><text>amount</text></inputExpression>
              </input>
              <output id="output_{{decisionKey}}" name="result" typeRef="string" />
              <rule id="high_{{decisionKey}}">
                <inputEntry><text>&gt; 100</text></inputEntry>
                <outputEntry><text>"high"</text></outputEntry>
              </rule>
              <rule id="low_{{decisionKey}}">
                <inputEntry><text>[0..100]</text></inputEntry>
                <outputEntry><text>"low"</text></outputEntry>
              </rule>
            </decisionTable>
          </decision>
          <dmndi:DMNDI>
            <dmndi:DMNDiagram id="diagram_{{decisionKey}}">
              <dmndi:DMNShape id="shape_{{decisionKey}}" dmnElementRef="{{decisionKey}}">
                <dc:Bounds x="160" y="80" width="180" height="80" />
              </dmndi:DMNShape>
            </dmndi:DMNDiagram>
          </dmndi:DMNDI>
        </definitions>
        """;

    private static string CreateN8nWorkflow(string workflowName) => $$"""
        {
          "name": "{{workflowName}}",
          "nodes": [
            { "name": "Webhook", "type": "n8n-nodes-base.webhook", "parameters": {} },
            {
              "name": "Request",
              "type": "n8n-nodes-base.httpRequest",
              "parameters": { "url": "https://example.invalid/orders", "method": "GET" },
              "credentials": { "httpBasicAuth": { "id": "legacy-missing", "name": "Legacy HTTP" } }
            }
          ],
          "connections": {
            "Webhook": { "main": [[{ "node": "Request", "type": "main", "index": 0 }]] }
          }
        }
        """;

    private static string CreateFormJson(string runId) => $$"""
        {
          "schemaVersion": 1,
          "type": "default",
          "components": [
            {
              "type": "textfield",
              "key": "customer-{{runId}}",
              "label": "Customer number",
              "id": "field_{{runId}}"
            }
          ]
        }
        """;

    private static string CreateCmmn(
        string caseKey,
        string reviewId,
        string milestoneId,
        string eventId,
        string caseFileItemId) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <cmmn:definitions xmlns:cmmn="http://www.omg.org/spec/CMMN/20151109/MODEL" id="definitions-{{caseKey}}" targetNamespace="https://vertexbpmn.io/local-e2e">
          <cmmn:case id="{{caseKey}}" name="Local Studio E2E case">
            <cmmn:caseFileModel id="case-file-{{caseKey}}">
              <cmmn:caseFileItem id="{{caseFileItemId}}" name="Customer" />
            </cmmn:caseFileModel>
            <cmmn:casePlanModel id="plan-{{caseKey}}" name="Local Studio E2E plan">
              <cmmn:planItem id="{{reviewId}}" definitionRef="review-definition-{{caseKey}}" />
              <cmmn:planItem id="{{milestoneId}}" definitionRef="milestone-definition-{{caseKey}}" />
              <cmmn:planItem id="event-item-{{caseKey}}" definitionRef="{{eventId}}" />
              <cmmn:humanTask id="review-definition-{{caseKey}}" name="Review customer" />
              <cmmn:milestone id="milestone-definition-{{caseKey}}" name="Case opened" />
              <cmmn:userEventListener id="{{eventId}}" name="Approve case" />
              <cmmn:planningTable id="planning-{{caseKey}}">
                <cmmn:discretionaryItem id="optional-review-{{caseKey}}" definitionRef="optional-definition-{{caseKey}}" />
              </cmmn:planningTable>
              <cmmn:humanTask id="optional-definition-{{caseKey}}" name="Optional review" />
            </cmmn:casePlanModel>
          </cmmn:case>
        </cmmn:definitions>
        """;
}
