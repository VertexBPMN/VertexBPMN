using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

// Browser E2E methods drive real multi-step UI flows and are intentionally long
// (consistent with the repo's pragma suppression of MA0051 for long methods).
#pragma warning disable MA0051

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// LC07 S1 browser acceptance: the Studio low-code application designer's core flow
/// create application - add page - add component - edit property - save - reload.
/// Mirrors the FormBuilder_ImportsSavesReloadsAndExports pattern (real backend + tenant).
/// </summary>
public sealed partial class LocalStudioInfrastructureTests
{
	private static readonly string[] DesignerInvoiceFields = ["number", "amount", "customer"];
	[Fact]
	public async Task ApplicationDesigner_CreateAddPageAddComponentEditPropertySaveAndReload_ARealDraft()
	{
		Assert.SkipUnless(
			LocalStudioE2ETestHost.IsEnabled,
			"Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var tenantName = $"Studio E2E LC07 {host.RunId}";
		var appKey = $"lc07-designer-{host.RunId}";
		string? tenantId = null;
		string? appId = null;

		using var apiClient = host.CreateApiClient();
		var page = await host.CreatePageAsync();
		var browserErrors = new ConcurrentQueue<string>();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			tenantId = await CreateDesignerTenantAsync(apiClient, tenantName);

			// Applications page: open it, switch to the freshly created tenant.
			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
			await SelectTenantAsync(page, tenantName, tenantId!);

			// CREATE: key + name, then create (navigates into the designer).
			await FillBoundInputAsync(page.GetByTestId("app-key"), appKey);
			await FillBoundInputAsync(page.GetByTestId("app-name"), "LC07 designer draft");
			await page.GetByTestId("create-app").ClickAsync();
			await page.GetByTestId("new-page-title").WaitForAsync();
			await page.GetByText("Select a page first.", new() { Exact = true }).WaitForAsync();

			// ADD PAGE: title "Home" then Add (auto-selects it).
			await FillBoundInputAsync(page.GetByTestId("new-page-title"), "Home");
			await page.GetByTestId("add-page").ClickAsync();
			await page.GetByTestId("page-node").WaitForAsync();

			// ADD COMPONENT: a text component from the closed palette.
			await page.GetByTestId("palette-text").ClickAsync();
			await page.GetByTestId("prop-label").WaitForAsync();

			// EDIT PROPERTY: set the text label to "Welcome".
			await FillBoundInputAsync(page.GetByTestId("prop-label"), "Welcome");

			// SAVE: persist the draft through the real Application API.
			await page.GetByTestId("save-draft").ClickAsync();
			await page.GetByText("Draft saved as revision 2.", new() { Exact = true }).WaitForAsync();

			// Verify via the REAL API that the content was persisted with the edited label.
			using var listResponse = await apiClient.GetAsync(
				$"api/applications?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
			listResponse.EnsureSuccessStatusCode();
			using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var row = listJson.RootElement.EnumerateArray().Single(item => item.GetProperty("key").GetString() == appKey);
			appId = row.GetProperty("id").GetString();

			using var getResponse = await apiClient.GetAsync(
				$"api/applications/{Uri.EscapeDataString(appId!)}?tenantId={Uri.EscapeDataString(tenantId!)}",
				TestContext.Current.CancellationToken);
			getResponse.EnsureSuccessStatusCode();
			using var appJson = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var content = appJson.RootElement.GetProperty("content").GetString()!;
			await AssertDesignerDatabaseAsync(appId!, tenantId!, content);
			Assert.Equal(2, appJson.RootElement.GetProperty("version").GetInt32());
			Assert.Contains("\"route\":\"/home\"", content, StringComparison.Ordinal);
			Assert.Contains("\"type\":\"text\",\"version\":\"1.0.0\",\"label\":\"Welcome\"", content, StringComparison.Ordinal);

			// RELOAD: re-open the designer; the edited property must still be there.
			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await SelectTenantAsync(page, tenantName, tenantId!);
			await page.GetByTestId("app-key").WaitForAsync();
			await page.EvaluateAsync("url => Blazor.navigateTo(url)", $"/applications/{Uri.EscapeDataString(appId!)}/design");
			await page.GetByTestId("new-page-title").WaitForAsync();
			await page.GetByTestId("component-node").GetByRole(AriaRole.Button).First.ClickAsync();
			await page.GetByTestId("prop-label").WaitForAsync();
			var reloadedValue = await page.GetByTestId("prop-label").InputValueAsync();
			Assert.Equal("Welcome", reloadedValue);

			// Switching tenants must invalidate the loaded draft, not expose A's editor in B.
			await FillBoundInputAsync(page.GetByTestId("prop-label"), "Unsaved tenant A edit");
			await SelectTenantAsync(page, host.AgentTenantName, host.TenantId);
			Assert.True(await page.GetByTestId("save-draft").IsDisabledAsync());
			Assert.Equal(0, await page.GetByTestId("component-node").CountAsync());

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/applications/{Uri.EscapeDataString(appId)}?tenantId={Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}

			if (!string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/tenant/{Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
		}
	}

	private async Task AssertDesignerDatabaseAsync(string appId, string tenantId, string content)
	{
		var settings = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("VERTEXBPMN_E2E_BPMN_CONNECTION"));
		settings.Database = host.IsolatedDatabaseNames.Single(name => name.StartsWith(settings.Database + "_e2e_", StringComparison.Ordinal));
		await using var connection = new Npgsql.NpgsqlConnection(settings.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = """
			SELECT d."ContentJson", r."ContentJson", d."Version", r."Version"
			FROM "ApplicationDefinitions" d JOIN "ApplicationRevisions" r
			ON r."ApplicationDefinitionId" = d."Id" AND r."TenantId" = d."TenantId" AND r."Version" = d."Version"
			WHERE d."Id" = @id AND d."TenantId" = @tenant
			""";
		command.Parameters.AddWithValue("id", appId);
		command.Parameters.AddWithValue("tenant", tenantId);
		await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
		Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
		Assert.Equal(content, reader.GetString(0));
		Assert.Equal(content, reader.GetString(1));
		Assert.Equal(2, reader.GetInt32(2));
		Assert.Equal(2, reader.GetInt32(3));
		Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
		TestContext.Current.TestOutputHelper?.WriteLine("PostgreSQL: tenant-scoped draft and revision 2 exactly match API content.");
	}

	private static async Task<string?> CreateDesignerTenantAsync(HttpClient apiClient, string tenantName)
	{
		using var tenantResponse = await apiClient.PostAsJsonAsync(
			"api/tenant",
			new { name = tenantName, description = "LC07 designer browser test" },
			TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		using var tenantJson = JsonDocument.Parse(tenantBody);
		return tenantJson.RootElement.GetProperty("id").GetString();
	}

	/// <summary>
	/// LC07 S2 browser acceptance: declare a data source -> bind a table -> map a schema field
	/// -> save -> reload. Mirrors the S1 ApplicationDesigner flow and verifies the persisted draft
	/// (dataSources + bound table + field mapping) through the real API, then that it reloads.
	/// </summary>
	[Fact]
	public async Task ApplicationDesigner_DeclareDataSourceBindTableMapFieldSaveAndReload_ARealDraft()
	{
		Assert.SkipUnless(
			LocalStudioE2ETestHost.IsEnabled,
			"Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var tenantName = $"Studio E2E LC07-S2 {host.RunId}";
		var appKey = $"lc07s2designer-{host.RunId}";
		string? tenantId = null;
		string? appId = null;

		using var apiClient = host.CreateApiClient();
		var page = await host.CreatePageAsync();
		var browserErrors = new ConcurrentQueue<string>();
		page.PageError += (_, error) => browserErrors.Enqueue(error);

		try
		{
			tenantId = await CreateDesignerTenantAsync(apiClient, tenantName);

			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
			await SelectTenantAsync(page, tenantName, tenantId!);

			// CREATE the application (navigates into the designer).
			await FillBoundInputAsync(page.GetByTestId("app-key"), appKey);
			await FillBoundInputAsync(page.GetByTestId("app-name"), "LC07 S2 data binding draft");
			await page.GetByTestId("create-app").ClickAsync();
			await page.GetByTestId("new-page-title").WaitForAsync();

			// ADD PAGE + TABLE.
			await FillBoundInputAsync(page.GetByTestId("new-page-title"), "Home");
			await page.GetByTestId("add-page").ClickAsync();
			await page.GetByTestId("palette-table").ClickAsync();
			await page.Locator("[data-testid=prop-dataSource]:visible").WaitForAsync();

			// DECLARE DATA SOURCE: key + authored schema fields.
			await FillBoundInputAsync(page.GetByTestId("data-source-key"), "invoiceList");
			await FillBoundInputAsync(page.GetByTestId("data-source-fields"), "number, amount, customer");
			await page.GetByTestId("data-source-add").ClickAsync();
			await page.GetByTestId("data-source-node").WaitForAsync();

			// BIND TABLE to the declared source via the property-editor dataSource select.
			await page.Locator("[data-testid=prop-dataSource]:visible").ClickAsync();
			await page.GetByRole(AriaRole.Option, new() { Name = "invoiceList" }).ClickAsync();

			// MAP FIELD: table column "Number" <- source field "number" (schema-validated).
			await page.Locator("[data-testid=mapping-source]:visible").ClickAsync();
			Assert.Equal(DesignerInvoiceFields, await page.GetByRole(AriaRole.Option).AllTextContentsAsync());
			await page.GetByRole(AriaRole.Option, new() { Name = "number", Exact = true }).ClickAsync();
			await FillBoundInputAsync(page.GetByTestId("mapping-target"), "Number");
			await page.GetByTestId("mapping-add").ClickAsync();
			await page.GetByTestId("mapping-node").WaitForAsync();

			// SAVE through the real Application API.
			await page.GetByTestId("save-draft").ClickAsync();
			await page.GetByText("Draft saved as revision 2.", new() { Exact = true }).WaitForAsync();

			// Verify via the REAL API that the persisted content has dataSources + bound table + mapping.
			using var listResponse = await apiClient.GetAsync(
				$"api/applications?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
			listResponse.EnsureSuccessStatusCode();
			using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var row = listJson.RootElement.EnumerateArray().Single(item => item.GetProperty("key").GetString() == appKey);
			appId = row.GetProperty("id").GetString();

			using var getResponse = await apiClient.GetAsync(
				$"api/applications/{Uri.EscapeDataString(appId!)}?tenantId={Uri.EscapeDataString(tenantId!)}",
				TestContext.Current.CancellationToken);
			getResponse.EnsureSuccessStatusCode();
			using var appJson = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var content = appJson.RootElement.GetProperty("content").GetString()!;
			await AssertDesignerDatabaseAsync(appId!, tenantId!, content);
			Assert.Contains("\"dataSources\":[{\"key\":\"invoiceList\"", content, StringComparison.Ordinal);
			Assert.Contains("\"dataSource\":\"invoiceList\"", content, StringComparison.Ordinal);
			Assert.Contains("\"sourcePath\":\"number\",\"targetKey\":\"Number\"", content, StringComparison.Ordinal);

			// RELOAD the designer; the declared source must still be present.
			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await SelectTenantAsync(page, tenantName, tenantId!);
			await page.GetByTestId("app-key").WaitForAsync();
			await page.EvaluateAsync("url => Blazor.navigateTo(url)", $"/applications/{Uri.EscapeDataString(appId!)}/design");
			await page.GetByTestId("new-page-title").WaitForAsync();
			await page.GetByTestId("data-source-node").WaitForAsync();
			await page.GetByTestId("component-node").GetByRole(AriaRole.Button).First.ClickAsync();
			Assert.Equal("invoiceList", await page.Locator("[data-testid=prop-dataSource]:visible").InnerTextAsync());
			Assert.Contains("number", await page.GetByTestId("mapping-node").InnerTextAsync(), StringComparison.Ordinal);
			Assert.Contains("Number", await page.GetByTestId("mapping-node").InnerTextAsync(), StringComparison.Ordinal);

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/applications/{Uri.EscapeDataString(appId)}?tenantId={Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
			if (!string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/tenant/{Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
		}
	}

	/// <summary>
	/// LC07 S3 browser acceptance: add a button -> add an action (data-source-read) from the
	/// CLOSED catalogue -> select its reference target (a DECLARED data source) -> set an
	/// explicit input -> save -> reload, asserted against the real API + PostgreSQL. Then the
	/// negative: removing the referenced data source leaves the action dangling and the
	/// designer flags UNKNOWN_DATA_SOURCE (never silently accepted).
	/// </summary>
	[Fact]
	public async Task ApplicationDesigner_ConfigureActionOnButtonSaveReloadAndFlagDanglingReference_ARealDraft()
	{
		Assert.SkipUnless(
			LocalStudioE2ETestHost.IsEnabled,
			"Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var tenantName = $"Studio E2E LC07-S3 {host.RunId}";
		var appKey = $"lc07s3designer-{host.RunId}";
		string? tenantId = null;
		string? appId = null;

		using var apiClient = host.CreateApiClient();
		var page = await host.CreatePageAsync();
		var browserErrors = new ConcurrentQueue<string>();
		page.PageError += (_, error) => browserErrors.Enqueue(error);

		try
		{
			tenantId = await CreateDesignerTenantAsync(apiClient, tenantName);

			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
			await SelectTenantAsync(page, tenantName, tenantId!);

			// CREATE the application, add a page and a BUTTON component.
			await FillBoundInputAsync(page.GetByTestId("app-key"), appKey);
			await FillBoundInputAsync(page.GetByTestId("app-name"), "LC07 S3 action draft");
			await page.GetByTestId("create-app").ClickAsync();
			await page.GetByTestId("new-page-title").WaitForAsync();

			await FillBoundInputAsync(page.GetByTestId("new-page-title"), "Home");
			await page.GetByTestId("add-page").ClickAsync();
			await page.GetByTestId("page-node").WaitForAsync();
			await page.GetByTestId("palette-button").ClickAsync();
			await page.GetByTestId("prop-label").WaitForAsync();

			// DECLARE a data source (the reference target the action binds to).
			await FillBoundInputAsync(page.GetByTestId("data-source-key"), "invoiceList");
			await FillBoundInputAsync(page.GetByTestId("data-source-fields"), "number, amount, customer");
			await page.GetByTestId("data-source-add").ClickAsync();
			await page.GetByTestId("data-source-node").WaitForAsync();

			// ADD ACTION: pick data-source-read from the CLOSED catalogue, then Add.
			await page.Locator("[data-testid=action-type]:visible").ClickAsync();
			await page.GetByRole(AriaRole.Option, new() { Name = "data-source-read", Exact = true }).ClickAsync();
			await page.GetByTestId("action-add").ClickAsync();

			// SET REFERENCE TARGET: select the DECLARED data source.
			await page.Locator("[data-testid=action-target]:visible").ClickAsync();
			await page.GetByRole(AriaRole.Option, new() { Name = "invoiceList", Exact = true }).ClickAsync();

			// SET EXPLICIT INPUT.
			await FillBoundInputAsync(page.GetByTestId("action-input-name"), "maxRecords");
			await FillBoundInputAsync(page.GetByTestId("action-input-value"), "50");
			await page.GetByTestId("action-input-add").ClickAsync();
			await page.GetByTestId("action-input-node").WaitForAsync();

			// SAVE through the real Application API.
			await page.GetByTestId("save-draft").ClickAsync();
			await page.GetByText("Draft saved as revision 2.", new() { Exact = true }).WaitForAsync();

			// Verify via the REAL API that the persisted content holds the configured action.
			using var listResponse = await apiClient.GetAsync(
				$"api/applications?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
			listResponse.EnsureSuccessStatusCode();
			using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var row = listJson.RootElement.EnumerateArray().Single(item => item.GetProperty("key").GetString() == appKey);
			appId = row.GetProperty("id").GetString();

			using var getResponse = await apiClient.GetAsync(
				$"api/applications/{Uri.EscapeDataString(appId!)}?tenantId={Uri.EscapeDataString(tenantId!)}",
				TestContext.Current.CancellationToken);
			getResponse.EnsureSuccessStatusCode();
			using var appJson = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var content = appJson.RootElement.GetProperty("content").GetString()!;
			await AssertDesignerDatabaseAsync(appId!, tenantId!, content);
			Assert.Contains("\"action\":{\"type\":\"data-source-read\",\"version\":\"1.0.0\",\"dataSource\":\"invoiceList\",\"inputs\":{\"maxRecords\":\"50\"}}", content, StringComparison.Ordinal);
			Assert.Equal("[]", VertexBPMN.Domain.Entities.ApplicationDomainDiagnostics.Validate(content));

			// RELOAD the designer; the confirmed action + reference + input must all be present.
			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await SelectTenantAsync(page, tenantName, tenantId!);
			await page.GetByTestId("app-key").WaitForAsync();
			await page.EvaluateAsync("url => Blazor.navigateTo(url)", $"/applications/{Uri.EscapeDataString(appId!)}/design");
			await page.GetByTestId("new-page-title").WaitForAsync();
			await page.GetByTestId("component-node").GetByRole(AriaRole.Button).First.ClickAsync();
			await page.GetByTestId("action-remove").WaitForAsync(); // action survived the reload
			Assert.Equal("invoiceList", await page.Locator("[data-testid=action-target]:visible").InnerTextAsync());
			Assert.Contains("maxRecords", await page.GetByTestId("action-input-node").InnerTextAsync(), StringComparison.Ordinal);

			// NEGATIVE: removing the referenced data source leaves the action dangling. The
			// designer surfaces UNKNOWN_DATA_SOURCE instead of silently accepting the breakage.
			await page.GetByTestId("data-source-delete").First.ClickAsync();
			await page.GetByText("unresolved diagnostics", new() { Exact = false }).WaitForAsync();
			Assert.Contains("UNKNOWN_DATA_SOURCE", await page.GetByText("unresolved diagnostics", new() { Exact = false }).InnerTextAsync(), StringComparison.Ordinal);

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/applications/{Uri.EscapeDataString(appId)}?tenantId={Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
			if (!string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/tenant/{Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
		}
	}

	[Fact]
	public async Task ApplicationDesigner_UndoRedoComponentAddition_KeepsDraftConsistent()
	{
		Assert.SkipUnless(
			LocalStudioE2ETestHost.IsEnabled,
			"Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var tenantName = $"Studio E2E LC07-S4-Undo {host.RunId}";
		var appKey = $"lc07s4undoredo-{host.RunId}";
		string? tenantId = null;
		string? appId = null;

		using var apiClient = host.CreateApiClient();
		var page = await host.CreatePageAsync();
		var browserErrors = new ConcurrentQueue<string>();
		page.PageError += (_, error) => browserErrors.Enqueue(error);

		try
		{
			tenantId = await CreateDesignerTenantAsync(apiClient, tenantName);
			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
			await SelectTenantAsync(page, tenantName, tenantId!);

			// CREATE the app, add a page and a TEXT component, then SAVE so the baseline
			// revision holds the text component (draft is clean = "Gespeichert").
			await FillBoundInputAsync(page.GetByTestId("app-key"), appKey);
			await FillBoundInputAsync(page.GetByTestId("app-name"), "LC07 S4 undo/redo draft");
			await page.GetByTestId("create-app").ClickAsync();
			await page.GetByTestId("new-page-title").WaitForAsync();

			await FillBoundInputAsync(page.GetByTestId("new-page-title"), "Home");
			await page.GetByTestId("add-page").ClickAsync();
			await page.GetByTestId("page-node").WaitForAsync();
			await page.GetByTestId("palette-text").ClickAsync();
			await page.GetByTestId("prop-label").WaitForAsync();

			await page.GetByTestId("save-draft").ClickAsync();
			await page.GetByText("Draft saved as revision 2.", new() { Exact = true }).WaitForAsync();
			Assert.Equal(1, await page.GetByTestId("component-node").CountAsync());

			// ADD a BUTTON: now two components, draft is dirty.
			await page.GetByTestId("palette-button").ClickAsync();
			await page.GetByText("Unstete Änderungen", new() { Exact = true }).WaitForAsync();
			Assert.Equal(2, await page.GetByTestId("component-node").CountAsync());

			// UNDO removes the button -> back to the saved baseline, draft clean again.
			await page.GetByTestId("undo").ClickAsync();
			await page.GetByText("Gespeichert", new() { Exact = true }).WaitForAsync();
			Assert.Equal(1, await page.GetByTestId("component-node").CountAsync());

			// REDO restores the button -> dirty again.
			await page.GetByTestId("redo").ClickAsync();
			await page.GetByText("Unstete Änderungen", new() { Exact = true }).WaitForAsync();
			Assert.Equal(2, await page.GetByTestId("component-node").CountAsync());

			// KEYBOARD shortcut (Ctrl+Z) is wired and behaves like the toolbar Undo.
			// Focus inside the surface via a pure selection click (no edit, so the button-add
			// stays the undo-top), then Ctrl+Z must remove the button and re-clean the draft.
			await page.GetByTestId("component-node").First.ClickAsync();
			await page.Keyboard.PressAsync("Control+z");
			await page.GetByText("Gespeichert", new() { Exact = true }).WaitForAsync();
			Assert.Equal(1, await page.GetByTestId("component-node").CountAsync());

			// The button addition was never persisted: undo/redo only moves the IN-MEMORY
			// draft. Verify via the real API that the saved revision is unchanged (rev 2, no button).
			using var listResponse = await apiClient.GetAsync(
				$"api/applications?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
			listResponse.EnsureSuccessStatusCode();
			using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var row = listJson.RootElement.EnumerateArray().Single(item => item.GetProperty("key").GetString() == appKey);
			appId = row.GetProperty("id").GetString();

			using var getResponse = await apiClient.GetAsync(
				$"api/applications/{Uri.EscapeDataString(appId!)}?tenantId={Uri.EscapeDataString(tenantId!)}",
				TestContext.Current.CancellationToken);
			getResponse.EnsureSuccessStatusCode();
			using var appJson = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
			var content = appJson.RootElement.GetProperty("content").GetString()!;
			Assert.Equal(2, appJson.RootElement.GetProperty("version").GetInt32());
			Assert.DoesNotContain("\"type\":\"button\"", content, StringComparison.Ordinal);
			Assert.Contains("\"type\":\"text\"", content, StringComparison.Ordinal);
			// The undo/redo stack did not re-save stale snapshots: the persisted revision matches the API.
			await AssertDesignerDatabaseAsync(appId!, tenantId!, content);

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/applications/{Uri.EscapeDataString(appId)}?tenantId={Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
			if (!string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/tenant/{Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
		}
	}

	[Fact]
	public async Task ApplicationDesigner_PreviewRendersDraft_WithoutFiringProductiveStart()
	{
		Assert.SkipUnless(
			LocalStudioE2ETestHost.IsEnabled,
			"Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var tenantName = $"Studio E2E LC07-S4-Preview {host.RunId}";
		var appKey = $"lc07s4preview-{host.RunId}";
		var processKey = $"lc07s4flow-{host.RunId}";
		string? tenantId = null;
		string? appId = null;

		using var apiClient = host.CreateApiClient();
		var page = await host.CreatePageAsync();
		var browserErrors = new ConcurrentQueue<string>();
		page.PageError += (_, error) => browserErrors.Enqueue(error);

		try
		{
			tenantId = await CreateDesignerTenantAsync(apiClient, tenantName);

			// Seed a real process definition into THIS tenant so the designer's process
			// catalogue knows a start-process key it can bind (never the hand-typed GUID).
			using (var deployResponse = await apiClient.PostAsJsonAsync(
				"api/repository",
				new { bpmnXml = CreateBpmn(processKey), name = $"{processKey}.bpmn", tenantId },
				TestContext.Current.CancellationToken))
			{
				var deployBody = await deployResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
				Assert.True(deployResponse.IsSuccessStatusCode, deployBody);
				Assert.Contains(processKey, deployBody, StringComparison.Ordinal);
			}
			host.RegisterProcessDefinitionCleanup(processKey);

			// Baseline: NO running process instances for this tenant yet.
			using (var runtimeResponse = await apiClient.GetAsync(
				$"api/runtime?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken))
			{
				runtimeResponse.EnsureSuccessStatusCode();
				using var runtimeJson = JsonDocument.Parse(await runtimeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
				Assert.Empty(runtimeJson.RootElement.EnumerateArray());
			}

			// BUILD the app: page + button + a data source + a start-process action bound to
			// the tenant's deployed process key. Everything lives in the in-memory DRAFT —
			// we deliberately do NOT persist the start-process action (the released server
			// gate requires process references as GUIDs, which the draft stores by key; that
			// is an S3/server concern, out of S4 scope). The preview renders this DRAFT.
			await page.GotoAsync($"{host.StudioBaseAddress}applications");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
			await SelectTenantAsync(page, tenantName, tenantId!);

			await FillBoundInputAsync(page.GetByTestId("app-key"), appKey);
			await FillBoundInputAsync(page.GetByTestId("app-name"), "LC07 S4 preview draft");
			await page.GetByTestId("create-app").ClickAsync();
			await page.GetByTestId("new-page-title").WaitForAsync();

			await FillBoundInputAsync(page.GetByTestId("new-page-title"), "Home");
			await page.GetByTestId("add-page").ClickAsync();
			await page.GetByTestId("page-node").WaitForAsync();
			await page.GetByTestId("palette-button").ClickAsync();
			await page.GetByTestId("prop-label").WaitForAsync();

			// Declare a data source so the preview shows a redacted test-data snapshot.
			await FillBoundInputAsync(page.GetByTestId("data-source-key"), "invoiceList");
			await FillBoundInputAsync(page.GetByTestId("data-source-fields"), "number, amount");
			await page.GetByTestId("data-source-add").ClickAsync();
			await page.GetByTestId("data-source-node").WaitForAsync();

			// Add a START-PROCESS action and bind it to the deployed process key.
			await page.Locator("[data-testid=action-type]:visible").ClickAsync();
			await page.GetByRole(AriaRole.Option, new() { Name = "start-process", Exact = true }).ClickAsync();
			await page.GetByTestId("action-add").ClickAsync();
			await page.Locator("[data-testid=action-target]:visible").ClickAsync();
			await page.GetByRole(AriaRole.Option, new() { Name = processKey, Exact = true }).ClickAsync();
			Assert.Equal(0, await page.GetByTestId("preview-panel").CountAsync());

			// Capture the persisted app (revision 1 from create) BEFORE previewing.
			using (var listResponse = await apiClient.GetAsync(
				$"api/applications?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken))
			{
				listResponse.EnsureSuccessStatusCode();
				using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
				var row = listJson.RootElement.EnumerateArray().Single(item => item.GetProperty("key").GetString() == appKey);
				appId = row.GetProperty("id").GetString();
			}
			string baselineContent;
			using (var getResponse = await apiClient.GetAsync(
				$"api/applications/{Uri.EscapeDataString(appId!)}?tenantId={Uri.EscapeDataString(tenantId!)}",
				TestContext.Current.CancellationToken))
			{
				getResponse.EnsureSuccessStatusCode();
				using var appJson = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
				baselineContent = appJson.RootElement.GetProperty("content").GetString()!;
				Assert.Equal(1, appJson.RootElement.GetProperty("version").GetInt32());
			}

			// OPEN the preview with an EXPLICITLY selected test target, then RUN it.
			// MudSelect emits BOTH a hidden <input> and a visible <div> tagged preview-target;
			// target the visible element (strict mode would otherwise flag 2 matches).
			await page.GetByTestId("preview-open").ClickAsync();
			await page.GetByTestId("preview-panel").WaitForAsync();
			await page.GetByTestId("preview-banner").WaitForAsync();
			await page.Locator("[data-testid=preview-target]:visible").WaitForAsync();
			await page.GetByTestId("preview-run").ClickAsync();

			// The preview renders the CURRENT DRAFT: pages, allowlist nodes, the
			// non-executing marker on the start-process button, and the redacted test data.
			await page.GetByTestId("preview-result").WaitForAsync();
			Assert.Equal(1, await page.GetByTestId("preview-page").CountAsync());
			Assert.Equal(1, await page.GetByTestId("preview-node").CountAsync());
			Assert.True(await page.GetByTestId("preview-non-executing").CountAsync() >= 1);
			Assert.True(await page.GetByTestId("preview-redacted").CountAsync() >= 2);
			var bannerText = await page.GetByTestId("preview-banner").InnerTextAsync();
			Assert.Contains("keine", bannerText, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("redigierten Testdaten", bannerText, StringComparison.OrdinalIgnoreCase);

			// INDEPENDENT non-executing proof (not a UI success message): after previewing a
			// draft whose button would START a process in production, NO process instance was
			// created, NO task was opened for this tenant, and NO draft write happened.
			using (var runtimeAfter = await apiClient.GetAsync(
				$"api/runtime?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken))
			{
				runtimeAfter.EnsureSuccessStatusCode();
				using var runJson = JsonDocument.Parse(await runtimeAfter.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
				Assert.Empty(runJson.RootElement.EnumerateArray());
			}
			using (var getAfter = await apiClient.GetAsync(
				$"api/applications/{Uri.EscapeDataString(appId!)}?tenantId={Uri.EscapeDataString(tenantId!)}",
				TestContext.Current.CancellationToken))
			{
				getAfter.EnsureSuccessStatusCode();
				using var appAfter = JsonDocument.Parse(await getAfter.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
				Assert.Equal(1, appAfter.RootElement.GetProperty("version").GetInt32());
				Assert.Equal(baselineContent, appAfter.RootElement.GetProperty("content").GetString());
			}

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/applications/{Uri.EscapeDataString(appId)}?tenantId={Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
			if (!string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/tenant/{Uri.EscapeDataString(tenantId)}",
					TestContext.Current.CancellationToken);
			}
		}
	}
}
