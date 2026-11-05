using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// FINAL LC07 slice S5 / LC-AC-09 browser acceptance (Kriterium B — real browser/API/DB,
/// independent of UI success messages). A persona authors the reference application entirely
/// through the LC07 designer (searchable table + field mapping + button whose start-process
/// action fires), SAVEs it (S5 gate fix persists the action), reloads, PUBLISHES it via
/// "Veröffentlichen", then runs the portal path: the released revision is served, a REAL
/// process instance starts by key, and the FinanceAgent approval task is claimed + completed.
/// Reuses the exact S1-S4 authoring selectors; asserts every criterion against API + Postgres.
/// </summary>
public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    public async Task ApplicationDesignerLc09_AuthorSavePublishAndRunReferenceApp_RealBrowserApiDb()
    {
        Assert.SkipUnless(
            LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        var tenantName = $"Studio E2E LC07-S5-LC09 {host.RunId}";
        var appKey = $"lc07s5lc09-{host.RunId}";
        var processKey = $"lc07s5lc09flow-{host.RunId}";

        using var apiClient = host.CreateApiClient();
        var page = await host.CreatePageAsync();
        var browserErrors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        page.PageError += (_, error) => browserErrors.Enqueue(error);

        var tenantId = (await CreateDesignerTenantAsync(apiClient, tenantName))!;
        string? appId = null;
        try
        {
            await DeployCandidateBpmnAsync(apiClient, tenantId, processKey);

            await AuthorInDesignerAsync(page, tenantName, tenantId, appKey, processKey);

            appId = await ResolveAppIdAsync(apiClient, tenantId, appKey);
            var savedContent = await GetSavedContentAsync(apiClient, appId, tenantId);
            VerifySavedContent(savedContent, processKey);

            await ReloadDesignerAsync(page, tenantName, tenantId, appId);
            await PublishInDesignerAsync(page);
            await VerifyReleasedAsync(apiClient, appId, tenantId, processKey);
            await AssertRelaseDbStateAsync(appId, tenantId, processKey);

            var instanceId = await StartRealProcessByKeyAsync(apiClient, processKey, tenantId);
            var taskId = await GetTaskFromDbAsync(instanceId, tenantId);
            await ClaimAndCompleteAsync(apiClient, taskId, tenantId);
            await AssertTaskCompletedInDbAsync(taskId);

            Assert.Empty(browserErrors);
        }
        finally
        {
            await host.ClosePageAsync(page);
            if (appId is not null && tenantId is not null)
            {
                await apiClient.DeleteAsync(
                    $"api/applications/{Uri.EscapeDataString(appId)}?tenantId={Uri.EscapeDataString(tenantId)}",
                    TestContext.Current.CancellationToken);
            }
            if (tenantId is not null)
            {
                await apiClient.DeleteAsync(
                    $"api/tenant/{Uri.EscapeDataString(tenantId)}",
                    TestContext.Current.CancellationToken);
            }
        }
    }

    /// <summary>Author the whole reference app through the designer, then save + reload.</summary>
    private async Task AuthorInDesignerAsync(IPage page, string tenantName, string tenantId, string appKey, string processKey)
    {
        await page.GotoAsync($"{host.StudioBaseAddress}applications");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
        await SelectTenantAsync(page, tenantName, tenantId);

        await FillBoundInputAsync(page.GetByTestId("app-key"), appKey);
        await FillBoundInputAsync(page.GetByTestId("app-name"), "LC07 S5 reference app");
        await page.GetByTestId("create-app").ClickAsync();
        await page.GetByTestId("new-page-title").WaitForAsync();

        // Page + searchable table bound to a declared data source.
        await FillBoundInputAsync(page.GetByTestId("new-page-title"), "Home");
        await page.GetByTestId("add-page").ClickAsync();
        await page.GetByTestId("palette-table").ClickAsync();
        await page.Locator("[data-testid=prop-dataSource]:visible").WaitForAsync();

        await FillBoundInputAsync(page.GetByTestId("data-source-key"), "invoiceList");
        await FillBoundInputAsync(page.GetByTestId("data-source-fields"), "number, amount, customer");
        await page.GetByTestId("data-source-add").ClickAsync();
        await page.GetByTestId("data-source-node").WaitForAsync();

        await page.Locator("[data-testid=prop-dataSource]:visible").ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "invoiceList" }).ClickAsync();

        // Data mapping: table column "Number" <- source field "number".
        await page.Locator("[data-testid=mapping-source]:visible").ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "number", Exact = true }).ClickAsync();
        await FillBoundInputAsync(page.GetByTestId("mapping-target"), "Number");
        await page.GetByTestId("mapping-add").ClickAsync();
        await page.GetByTestId("mapping-node").WaitForAsync();

        // Form: add a form-js component from the closed allowlist (no editable properties).
        await page.GetByTestId("palette-form-js").ClickAsync();

        // Button whose action starts the process, bound to the deployed key.
        await page.GetByTestId("palette-button").ClickAsync();
        await page.GetByTestId("prop-label").WaitForAsync();
        await page.Locator("[data-testid=action-type]:visible").ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "start-process", Exact = true }).ClickAsync();
        await page.GetByTestId("action-add").ClickAsync();
        await page.Locator("[data-testid=action-target]:visible").ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = processKey, Exact = true }).ClickAsync();

        // SAVE through the real Application API; start-process now persists (S5 gate fix).
        await page.GetByTestId("save-draft").ClickAsync();
        await page.GetByText("Draft saved as revision 2.", new() { Exact = true }).WaitForAsync();
    }

    private async Task ReloadDesignerAsync(IPage page, string tenantName, string tenantId, string appId)
    {
        await page.GotoAsync($"{host.StudioBaseAddress}applications");
        await SelectTenantAsync(page, tenantName, tenantId);
        await page.GetByTestId("app-key").WaitForAsync();
        await page.EvaluateAsync("url => Blazor.navigateTo(url)", $"/applications/{Uri.EscapeDataString(appId)}/design");
        await page.GetByTestId("new-page-title").WaitForAsync();
        await page.GetByTestId("data-source-node").WaitForAsync();
    }

    private static async Task PublishInDesignerAsync(IPage page)
    {
        // The publish button only enables once a draft is loaded; ClickAsync auto-waits.
        await page.GetByTestId("publish").ClickAsync();
        await page.GetByText("veröffentlicht.").First.WaitForAsync();
        TestContext.Current.TestOutputHelper?.WriteLine("Browser: Veröffentlichen published the revision.");
    }

    private async Task DeployCandidateBpmnAsync(HttpClient apiClient, string tenantId, string processKey)
    {
        using var deployResponse = await apiClient.PostAsJsonAsync(
            "api/repository",
            new { bpmnXml = CreateUserTaskCandidateBpmn(processKey), name = $"{processKey}.bpmn", tenantId },
            TestContext.Current.CancellationToken);
        var deployBody = await deployResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(deployResponse.IsSuccessStatusCode, deployBody);
        Assert.Contains(processKey, deployBody, StringComparison.Ordinal);
        host.RegisterProcessDefinitionCleanup(processKey);
    }

    private static async Task<string> ResolveAppIdAsync(HttpClient apiClient, string tenantId, string appKey)
    {
        using var listResponse = await apiClient.GetAsync(
            $"api/applications?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
        listResponse.EnsureSuccessStatusCode();
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var row = listJson.RootElement.EnumerateArray().Single(item => item.GetProperty("key").GetString() == appKey);
        return row.GetProperty("id").GetString()!;
    }

    private static async Task<string> GetSavedContentAsync(HttpClient apiClient, string appId, string tenantId)
    {
        using var getResponse = await apiClient.GetAsync(
            $"api/applications/{Uri.EscapeDataString(appId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        using var appJson = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return appJson.RootElement.GetProperty("content").GetString()!;
    }

    /// <summary>S5 gate-fix proof: the persisted content carries the start-process action and a
    /// resolved {type,key,...} process reference (id+tenantId) that the server gate accepts.</summary>
    private static void VerifySavedContent(string savedContent, string processKey)
    {
        Assert.Contains("start-process", savedContent, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"form-js\"", savedContent, StringComparison.Ordinal);
        Assert.Contains("\"sourcePath\":\"number\",\"targetKey\":\"Number\"", savedContent, StringComparison.Ordinal);
        Assert.Contains("\"dataSource\":\"invoiceList\"", savedContent, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"process\"", savedContent, StringComparison.Ordinal);
        Assert.Contains($"\"key\":\"{processKey}\"", savedContent, StringComparison.Ordinal);
    }

    private static async Task VerifyReleasedAsync(HttpClient apiClient, string appId, string tenantId, string processKey)
    {
        using var releasedResponse = await apiClient.GetAsync(
            $"api/applications/{Uri.EscapeDataString(appId)}/released-revision?tenantId={Uri.EscapeDataString(tenantId)}",
            TestContext.Current.CancellationToken);
        releasedResponse.EnsureSuccessStatusCode();
        using var releasedJson = JsonDocument.Parse(await releasedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var releasedContent = releasedJson.RootElement.GetProperty("contentJson").GetString();
        Assert.NotNull(releasedContent);
        Assert.Contains("start-process", releasedContent, StringComparison.Ordinal);
        Assert.Contains($"\"key\":\"{processKey}\"", releasedContent, StringComparison.Ordinal);
    }

    private async Task<Guid> StartRealProcessByKeyAsync(HttpClient apiClient, string processKey, string tenantId)
    {
        using var startResponse = await apiClient.PostAsJsonAsync("api/runtime/start",
            new { ProcessDefinitionKey = processKey, Variables = (object?)null, BusinessKey = (object?)null, TenantId = tenantId },
            TestContext.Current.CancellationToken);
        var body = await startResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(startResponse.StatusCode == HttpStatusCode.Created, body);
        using var startJson = JsonDocument.Parse(body);
        var instanceId = startJson.RootElement.GetProperty("id").GetGuid();
        TestContext.Current.TestOutputHelper?.WriteLine($"API: real process instance started for key {processKey} -> {instanceId}.");
        return instanceId;
    }

    private async Task<Guid> GetTaskFromDbAsync(Guid instanceId, string tenantId)
    {
        await using var connection = NewPostgresConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "Id" FROM "Tasks"
            WHERE "ProcessInstanceId" = @instance AND "TenantId" = @tenant
            ORDER BY "CreatedAt" DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("instance", instanceId);
        command.Parameters.AddWithValue("tenant", tenantId);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(value);
        return (Guid)value;
    }

    private async Task ClaimAndCompleteAsync(HttpClient apiClient, Guid taskId, string tenantId)
    {
        using (var claimResponse = await apiClient.PostAsJsonAsync($"api/task/{taskId}/claim",
            new { UserId = "finance-user", TenantId = tenantId }, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, claimResponse.StatusCode);
        }
        using (var completeResponse = await apiClient.PostAsJsonAsync($"api/task/{taskId}/complete",
            new { Variables = new { }, TenantId = tenantId }, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, completeResponse.StatusCode);
        }
        TestContext.Current.TestOutputHelper?.WriteLine($"API: approval task {taskId} claimed and completed.");
    }

    private async Task AssertTaskCompletedInDbAsync(Guid taskId)
    {
        await using var connection = NewPostgresConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Assignee\", (\"CompletedAt\" IS NOT NULL) FROM \"Tasks\" WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", taskId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("finance-user", reader.GetString(0));
        Assert.True(reader.GetBoolean(1), "task was not completed in the database");
        TestContext.Current.TestOutputHelper?.WriteLine($"PostgreSQL: task {taskId} is Completed by finance-user.");
    }

    private async Task AssertRelaseDbStateAsync(string appId, string tenantId, string processKey)
    {
        await using var connection = NewPostgresConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r."ContentJson", r."DiagnosticsJson"
            FROM "ApplicationReleaseCandidates" c
            JOIN "ApplicationRevisions" r ON r."Id" = c."ApplicationRevisionId" AND r."TenantId" = c."TenantId"
            WHERE c."ApplicationDefinitionId" = @id AND c."TenantId" = @tenant AND c."IsReleased" = true
            """;
        command.Parameters.AddWithValue("id", appId);
        command.Parameters.AddWithValue("tenant", tenantId);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "released candidate row missing");
        var content = reader.GetString(0);
        Assert.Contains("\"type\":\"process\"", content, StringComparison.Ordinal);
        Assert.Contains($"\"key\":\"{processKey}\"", content, StringComparison.Ordinal);
        Assert.Contains("start-process", content, StringComparison.Ordinal);
        Assert.Contains("\"id\":\"", content, StringComparison.Ordinal);
        Assert.Contains($"\"tenantId\":\"{tenantId}\"", content, StringComparison.Ordinal);
        Assert.Equal("[]", reader.GetString(1));
        TestContext.Current.TestOutputHelper?.WriteLine("PostgreSQL: released candidate with resolved process reference + clean diagnostics persisted.");
    }

    private Npgsql.NpgsqlConnection NewPostgresConnection()
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("VERTEXBPMN_E2E_BPMN_CONNECTION"));
        settings.Database = host.IsolatedDatabaseNames.Single(name => name.StartsWith(settings.Database + "_e2e_", StringComparison.Ordinal));
        return new Npgsql.NpgsqlConnection(settings.ConnectionString);
    }

    private static string CreateUserTaskCandidateBpmn(string processKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_{{processKey}}" targetNamespace="https://vertexbpmn.io/local-e2e">
          <bpmn:process id="{{processKey}}" name="LC07 S5 reference flow" isExecutable="true">
            <bpmn:startEvent id="Start_{{processKey}}" />
            <bpmn:sequenceFlow id="Flow_Start_{{processKey}}" sourceRef="Start_{{processKey}}" targetRef="Approve_{{processKey}}" />
            <bpmn:userTask id="Approve_{{processKey}}" name="Freigabe" candidateRole="FinanceAgent" />
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
}
