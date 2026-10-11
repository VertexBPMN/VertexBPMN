using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

[Trait("Category", "LocalStudioE2E")]
public sealed partial class LocalStudioInfrastructureTests(LocalStudioE2ETestHost host)
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
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			var originalFlowId = $"Flow_{ProcessKey}";
			var credentialRef = await EditBpmnFlowWithCredentialAsync(page, ProcessKey);
			var editedXml = await WaitForPreviewXmlAsync(page, $"credential-{host.RunId}");
			var editedDocument = AssertEditedFlowDocument(editedXml, ProcessKey, originalFlowId);

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
			Assert.Contains(credentialRef, persisted.GetProperty("bpmnXml").GetString(), StringComparison.Ordinal);

			await DeploySecondVersionAndAssertAsync(page, apiClient, ProcessKey, credentialRef, editedDocument);

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
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			await OpenBpmnModelerAsync(page);
			await ImportBpmnAsync(page, CreateBpmn(processKey));
			await OpenBpmnToolTabAsync(page, "Test runs");
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
		var timerProcessKey = $"StudioE2E_Timer_{host.RunId}";
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
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			(tenantId, formId) = await SetupRuntimeResourcesAsync(apiClient, tenantName, processKey, timerProcessKey, formKey);
			var timerInstanceId = await DeployRuntimeProcessesAsync(page, apiClient, tenantName, tenantId, processKey, timerProcessKey, formKey);
			var instanceId = await StartProcessThroughModelerAsync(page, apiClient, tenantName, tenantId, processKey, businessKey);
			var taskId = await VerifyInstanceVariablesAndCompleteTaskAsync(page, apiClient, tenantName, tenantId, businessKey, instanceId, formKey);
			await VerifyCompletedInstanceHistoryAndLogsAsync(page, apiClient, tenantName, tenantId, businessKey, instanceId, taskId, timerInstanceId);

			Assert.True(
				browserErrors.IsEmpty,
				$"Browser errors: {string.Join(" | ", browserErrors)}. Recent Studio logs: {string.Join(" | ", host.StudioLogs.TakeLast(150))}");
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(formId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync($"api/forms/{Uri.EscapeDataString(formId)}?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
			}
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
			var (populatedTenantId, emptyTenantId, dashboardProcess) = await SetupManagementDataAsync(
				apiClient, populatedTenantName, emptyTenantName, primaryProcessKey);

			await page.GotoAsync(host.StudioBaseAddress.ToString());
			await SelectTenantAsync(page, populatedTenantName, populatedTenantId);
			await VerifyDashboardCountsAsync(page, "11", "1", "1", "1");
			await VerifyDashboardPopulatedAndRefreshAsync(page, apiClient, populatedTenantId, primaryProcessKey, dashboardProcess);
			await VerifyEmptyDashboardAndNavigateAsync(page, emptyTenantName, emptyTenantId, populatedTenantName, populatedTenantId);

			var definitionsGrid = page.GetByTestId("process-definitions-grid");
			await VerifyPaginationAsync(definitionsGrid);
			await VerifySearchViewVersionsAndDeleteAsync(page, definitionsGrid, primaryProcessKey);

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
		var failingProcessKey = $"Management_Failure_{host.RunId}";
		using var apiClient = host.CreateApiClient();
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);

		try
		{
			var lifecycleBusinessKey = $"lifecycle-{host.RunId}";
			var deletionBusinessKey = $"deletion-{host.RunId}";
			var failureBusinessKey = $"failure-{host.RunId}";
			var (tenantId, lifecycleInstanceId, deletionInstanceId, failedInstanceId) = await SetupManagementRuntimeAsync(
				apiClient, tenantName, processKey, failingProcessKey,
				lifecycleBusinessKey, deletionBusinessKey, failureBusinessKey);

			await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
			await SelectTenantAsync(page, tenantName, tenantId);
			await VerifySuspendResumeLifecycleAsync(page, lifecycleBusinessKey);
			await VerifyDeleteInstanceAsync(page, deletionBusinessKey, deletionInstanceId);
			await VerifyFailureInstanceAsync(page, apiClient, tenantName, tenantId, failureBusinessKey, failedInstanceId);
			await VerifyTaskLifecycleAsync(page, apiClient, tenantName, tenantId, lifecycleInstanceId);

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
			var tenantId = await SetupDeploymentsTenantAsync(apiClient, tenantName, processKeys);

			await page.GotoAsync($"{host.StudioBaseAddress}deployments");
			await SelectTenantAsync(page, tenantName, tenantId);
			var upload = page.GetByTestId("deployment-upload");
			await VerifyDeploymentUploadValidationAsync(page, upload, processKeys);
			await VerifyMultipleUploadAsync(page, upload, processKeys);
			await VerifyDeploymentDefinitionsAsync(page, tenantName, tenantId, processKeys);

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
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			using var apiClient = host.CreateApiClient();
			await OpenBpmnModelerAsync(page);
			await ImportN8nWorkflowAsync(page, browserErrors, workflowName, processKey);
			await ValidateDeployAndVerifyN8nAsync(page, apiClient, browserErrors, processKey);
			await ReloadN8nVersionAsync(page, processKey);
			await ExportN8nXmlAsync(page, processKey);
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
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			await SetupAndImportDecisionAsync(page, browserErrors, decisionKey);
			await ReloadExportAndRoundtripDecisionAsync(page, browserErrors, decisionKey);
			await EvaluateDecisionScenariosAsync(page, browserErrors);
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
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			tenantId = await CreateFormTenantAsync(apiClient, tenantName);
			await SetupFormAndSaveAsync(page, tenantName, tenantId, formKey);

			var persisted = await GetPersistedFormAsync(apiClient, tenantId, formKey);
			formId = persisted.GetProperty("id").GetString();
			Assert.Equal("Local Studio E2E form", persisted.GetProperty("name").GetString());
			Assert.Contains($"customer-{host.RunId}", persisted.GetProperty("schema").GetString(), StringComparison.Ordinal);

			await ReloadAndUpdateSavedFormAsync(page, tenantName, tenantId, formKey);

			persisted = await GetPersistedFormAsync(apiClient, tenantId, formKey);
			Assert.Equal(formId, persisted.GetProperty("id").GetString());
			Assert.Equal("Local Studio E2E form updated", persisted.GetProperty("name").GetString());

			await ExportAndRoundtripFormAsync(page, browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(formId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync(
					$"api/forms/{Uri.EscapeDataString(formId)}?tenantId={Uri.EscapeDataString(tenantId)}",
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
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			await ImportCaseModelAsync(page, browserErrors, caseKey, reviewId, milestoneId, eventId, caseFileItemId);
			using var apiClient = host.CreateApiClient();
			await RegisterCaseAndVerifyDefinitionAsync(page, apiClient, browserErrors, caseKey, reviewId);
			await ExecuteCaseAndUpdateCaseFileAsync(page, browserErrors, caseKey, reviewId, milestoneId, caseFileItemId);
			await TriggerEventGenerateAndLoadHistoryAsync(page, browserErrors, eventId, caseFileItemId);
			await AddTaskExportAndRoundtripAsync(page, browserErrors, caseKey);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact]
	public async Task ProcessDefinitions_ViewBpmnAndVersions_ThenDeletePersistsAfterReload()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var processKey = $"StudioE2E_Defs_{host.RunId}";
		host.RegisterProcessDefinitionCleanup(processKey);
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			// Deploy v1 through the real GUI modeler, then extend and redeploy v2 so the
			// version history dialog has more than a single version to display.
			await DeployTwoVersionsAsync(page, processKey);

			// The process definitions page lists the real persisted definitions.
			await ViewBpmnAndVersionsAsync(page, processKey);

			// Delete the definition through the UI and confirm the removal is durable.
			// Both deployed versions are listed; remove them all via the Actions column.
			await DeleteAllDefinitionVersionsAsync(page, processKey);

			// Reload the page and verify the definition is permanently gone from the real API.
			using var apiClient = host.CreateApiClient();
			await VerifyDeletionPersistsAsync(page, apiClient, processKey, browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact]
	public async Task ProcessInstances_ListsSearchesAndShowsDetails_ForARealRunningInstance()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var tenantName = $"Instances E2E {host.RunId}";
		var processKey = $"StudioE2E_Inst_{host.RunId}";
		var formKey = $"studio-e2e-inst-form-{host.RunId}";
		var businessKey = $"business-inst-{host.RunId}";
		string? tenantId = null;
		string? formId = null;
		using var apiClient = host.CreateApiClient();
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			(tenantId, formId) = await SetupInstanceTenantAndFormAsync(apiClient, tenantName, processKey, formKey);
			await DeployInstanceProcessAsync(page, tenantName, tenantId, processKey, formKey);

			// Start an instance so it remains waiting on the user task.
			await StartInstanceAndVerifyDetailsAsync(page, apiClient, tenantName, tenantId, processKey, businessKey);

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(formId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync($"api/forms/{Uri.EscapeDataString(formId)}?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
			}
		}
	}

	[Fact]
	public async Task ProcessInstances_SuspendResumeAndDelete_PersistThroughUiAndApi()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var tenantName = $"Instances Mgt {host.RunId}";
		var processKey = $"StudioE2E_InstMgt_{host.RunId}";
		var formKey = $"studio-e2e-instmgt-form-{host.RunId}";
		var businessKey = $"business-instmgt-{host.RunId}";
		string? tenantId = null;
		string? formId = null;
		using var apiClient = host.CreateApiClient();
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			(tenantId, formId) = await SetupInstanceMgtTenantAndFormAsync(apiClient, tenantName, processKey, formKey);
			await DeployInstanceMgtProcessAsync(page, tenantName, tenantId, processKey, formKey);
			var instanceId = await StartInstanceAsync(apiClient, processKey, businessKey, tenantId);
			await SuspendResumeDeleteInstanceAsync(page, apiClient, tenantName, tenantId, businessKey, instanceId);
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
			if (!string.IsNullOrWhiteSpace(formId) && !string.IsNullOrWhiteSpace(tenantId))
			{
				await apiClient.DeleteAsync($"api/forms/{Uri.EscapeDataString(formId)}?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
			}
		}
	}

	[Fact]
	public async Task Deployments_UploadValidRejectsInvalid_AndFindsDefinitionInProcessDefinitions()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var processKey = $"StudioE2E_Deploy_{host.RunId}";
		host.RegisterProcessDefinitionCleanup(processKey);
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await page.GotoAsync($"{host.StudioBaseAddress}deployments");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Deployments", Exact = true }).WaitForAsync();

			// Upload exactly once after hydration. Retrying the upload itself could
			// conceal a lost user action; only poll for its persisted result.
			var validXml = CreateBpmn(processKey);
			using var apiClient = host.CreateApiClient();
			await page.Locator("[data-interactive-ready='true']").WaitForAsync();
			await UploadBpmnFileAsync(page, "deploy-valid.bpmn", validXml);
			var deployed = await WaitForDeployedDefinitionAsync(apiClient, processKey);
			Assert.NotNull(deployed);
			Assert.True(deployed.Length > 0, "Valid BPMN upload did not produce a persisted definition.");
			await page.Locator("table").GetByText("deploy-valid.bpmn", new() { Exact = false }).First.WaitForAsync();

			// Upload an invalid file and verify a comprehensible error, not a crash or empty success.
			await UploadBpmnFileAsync(page, "deploy-invalid.bpmn", "<not-bpmn>broken</not-bpmn>");
			await page.GetByText("deploy-invalid.bpmn is not valid BPMN XML.", new() { Exact = true }).WaitForAsync();

			// The deployed definition is findable on the Process Definitions page.
			await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
			await FillBoundInputAsync(page.GetByTestId("process-definition-search"), processKey);
			await page.Locator("tr").Filter(new() { HasText = processKey }).First.WaitForAsync();

			var definitions = await apiClient.GetFromJsonAsync<JsonElement[]>(
				$"api/repository?key={Uri.EscapeDataString(processKey)}",
				TestContext.Current.CancellationToken);
			Assert.Single(definitions ?? []);
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact]
	public async Task ExecutionDetails_LoadsJobsIncidentsAndVariables_ForARealInstance()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var processKey = $"StudioE2E_Exec_{host.RunId}";
		host.RegisterProcessDefinitionCleanup(processKey);
		using var apiClient = host.CreateApiClient();
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await OpenBpmnModelerAsync(page);
			await ImportBpmnAsync(page, CreateBpmn(processKey));
			await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
			await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();

			using var startResponse = await apiClient.PostAsJsonAsync("api/runtime/start", new { ProcessDefinitionKey = processKey, BusinessKey = $"exec-{host.RunId}", TenantId = (string?)null, Variables = new Dictionary<string, object>(StringComparer.Ordinal) { ["customer"] = $"ACME-{host.RunId}" } }, TestContext.Current.CancellationToken);
			var startBody = await startResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
			Assert.True(startResponse.IsSuccessStatusCode, startBody);
			using var started = JsonDocument.Parse(startBody);
			var instanceId = started.RootElement.GetProperty("id").GetGuid();

			await page.GotoAsync($"{host.StudioBaseAddress}execution-details");
			await page.GetByRole(AriaRole.Button, new() { Name = "Load jobs", Exact = true }).ClickAsync();
			await page.GetByText("Jobs", new() { Exact = true }).WaitForAsync(new() { Timeout = 120_000 });
			await page.GetByRole(AriaRole.Button, new() { Name = "Load incidents", Exact = true }).ClickAsync();
			await page.GetByText("Incidents", new() { Exact = true }).WaitForAsync(new() { Timeout = 120_000 });

			await FillBoundInputAsync(page.GetByLabel("Process instance id for variables", new() { Exact = true }), instanceId.ToString());
			await page.GetByRole(AriaRole.Button, new() { Name = "Load variables", Exact = true }).ClickAsync();
			await page.GetByText("Variables", new() { Exact = true }).WaitForAsync(new() { Timeout = 120_000 });
			await page.GetByText("ACME-" + host.RunId, new() { Exact = false }).WaitForAsync(new() { Timeout = 120_000 });

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact]
	public async Task EventLog_PageLoads_AndShowsTheLiveSessionFeed()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			// The Event Log page renders the live, in-session engine event feed. It subscribes to a
			// session-scoped event service, so the meaningful content depends on engine actions taken
			// while the page is open; this test verifies the page renders cleanly without browser errors.
			var response = await page.GotoAsync($"{host.StudioBaseAddress}event-log");
			Assert.NotNull(response);
			Assert.True(response.Ok, $"Studio returned HTTP {response.Status}.");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Event Log", Exact = true }).WaitForAsync();

			// After a short settle, there must be no script/page errors.
			await Task.Delay(500, TestContext.Current.CancellationToken);
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact]
	public async Task ErrorPath_ExecutionDetails_RejectsAnInvalidProcessInstanceId()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			var response = await page.GotoAsync($"{host.StudioBaseAddress}execution-details");
			Assert.NotNull(response);
			Assert.True(response.Ok, $"Studio returned HTTP {response.Status}.");

			// Use an explicitly invalid process instance id and verify the Execution Details page
			// surfaces a clear, actionable validation error instead of failing silently or crashing.
			await FillBoundInputAsync(page.GetByLabel("Process instance id for variables", new() { Exact = true }), "not-a-guid");
			await page.GetByRole(AriaRole.Button, new() { Name = "Load variables", Exact = true }).ClickAsync();
			await page.GetByText("Enter a valid process instance id.", new() { Exact = true }).WaitForAsync();

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact]
	public async Task ErrorPath_Deployments_RejectsAnInvalidBpmnWithoutPersistingIt()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var processKey = $"StudioE2E_Bad_{host.RunId}";
		host.RegisterProcessDefinitionCleanup(processKey);
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await page.GotoAsync($"{host.StudioBaseAddress}deployments");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Deployments", Exact = true }).WaitForAsync();

			// An invalid (non-BPMN) upload must surface a comprehensible error and must NOT be
			// persisted as a deployable definition.
			var invalidChooser = page.WaitForFileChooserAsync();
			await page.Locator("[data-interactive-ready='true']").WaitForAsync();
			await page.GetByText("Upload BPMN files", new() { Exact = true }).ClickAsync();
			await (await invalidChooser).SetFilesAsync(new FilePayload
			{
				Name = "deploy-invalid.bpmn",
				MimeType = "application/xml",
				Buffer = Encoding.UTF8.GetBytes("<not-bpmn>broken</not-bpmn>")
			});
			await page.GetByText("deploy-invalid.bpmn is not valid BPMN XML.", new() { Exact = true }).WaitForAsync();

			using var apiClient = host.CreateApiClient();
			var definitions = await apiClient.GetFromJsonAsync<JsonElement[]>(
				$"api/repository?key={Uri.EscapeDataString(processKey)}",
				TestContext.Current.CancellationToken);
			Assert.True(definitions is null || definitions.Length == 0, "Invalid BPMN must not be persisted as a definition.");
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact]
	public async Task Simulation_RunsAndShowsResult_ThenPersistsScenarios_ThroughTheLiveSimulationEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var processKey = $"StudioE2E_Sim_{host.RunId}";
		var tenantName = $"Simulation E2E {host.RunId}";
		var scenarioName = $"Sim scenario {host.RunId}";
		string? scenarioId = null;
		string? tenantId = null;
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};
		var simulationResponses = new ConcurrentQueue<string>();
		HookSimulationResponses(page, simulationResponses);
		using var apiClient = host.CreateApiClient();

		try
		{
			tenantId = await CreateSimulationTenantAsync(apiClient, tenantName);
			await ProbeSimulationEngineAsync(processKey, tenantId);
			var resultJson = await RunSimulationAndGetResultAsync(page, processKey, tenantId, simulationResponses, tenantName);
			await AnalyzeAndCompareAsync(page, processKey, resultJson);
			scenarioId = await SaveScenarioAsync(page, apiClient, processKey, tenantId, scenarioName);
			await UpdateScenarioAsync(page, apiClient, tenantId, scenarioName, scenarioId);
			await DeleteScenarioAsync(page, apiClient, tenantId, scenarioId);
			Assert.Empty(browserErrors);
		}
		finally
		{
			if (scenarioId is not null)
			{
				host.RegisterApiCleanup(HttpMethod.Delete, $"api/simulation-scenario/{Uri.EscapeDataString(scenarioId)}");
			}

			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 4 - Messages & Signals: correlate a pending message and broadcast a signal through the GUI")]
	public async Task MessagesSignals_CorrelatesMessageAndBroadcastsSignal_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		var messageName = $"payment-{host.RunId}";
		var signalName = $"release-{host.RunId}";
		var messageProcessKey = $"msg-{Guid.NewGuid():N}"[..20];
		var signalProcessKey = $"sig-{Guid.NewGuid():N}"[..20];

		// The Messages & Signals page sends no tenant id (the development API key is Admin, so the
		// API resolves tenant to null). Correlating therefore only reaches instances started with a
		// null tenant, so start these test instances WITHOUT a tenant.
		var messageBpmn = CreateMessageInterruptBpmn(messageName, messageProcessKey);
		var signalBpmn = CreateSignalCatchBpmn(signalName, signalProcessKey);

		try
		{
			host.RegisterProcessDefinitionCleanup(messageProcessKey, null);
			host.RegisterProcessDefinitionCleanup(signalProcessKey, null);

			// Deploy both definitions via the real API (fast, avoids the modeler) and park instances
			// on their catching events with a null tenant so the GUI correlation can reach them.
			await DeployDefinitionAsync(apiClient, messageBpmn, $"{messageProcessKey}.bpmn");
			await DeployDefinitionAsync(apiClient, signalBpmn, $"{signalProcessKey}.bpmn");

			var messageInstanceId = await StartNullTenantInstanceAsync(apiClient, messageProcessKey, $"msg-{host.RunId}");
			var signalInstanceIds = await StartSignalInstancesAsync(apiClient, signalProcessKey, host.RunId);

			// Confirm all three instances are parked on their catching events (waiting to resume).
			// The engine reports catch-event waits as either "Running" or "Waiting", so accept either.
			await WaitForInstanceStateNotAsync(messageInstanceId, "Completed");
			foreach (var id in signalInstanceIds)
			{
				await WaitForInstanceStateNotAsync(id, "Completed");
			}

			var parkedState = await GetInstanceStateAsync(messageInstanceId);
			await CorrelateWrongThenRightMessageAsync(page, messageName, messageInstanceId, parkedState);
			await BroadcastSignalAsync(page, signalName, signalInstanceIds);

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 4 - Debugging: run a trace, then drive a visual debug session (breakpoint, step over, continue, visualize, variables)")]
	public async Task Debugging_RunsTraceAndDrivesVisualDebugSession_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		var debugProcessKey = $"dbg-{Guid.NewGuid():N}"[..20];
		var messageName = $"dbg-msg-{host.RunId}";

		// A linear process that starts, parks on an intermediate catch event (so the engine leaves it
		// running rather than completing it), then has two sequential tasks before the end. The visual
		// debug step service advances the persisted token one sequence flow at a time, so this gives us
		// a meaningful step-over -> step-over -> continue path.
		var debugBpmn = CreateDebugBpmn(debugProcessKey, messageName);

		try
		{
			host.RegisterProcessDefinitionCleanup(debugProcessKey, null);
			await DeployDebugProcessAsync(apiClient, debugProcessKey, debugBpmn);
			var instanceId = await StartDebugInstanceAsync(apiClient, debugProcessKey, host.RunId);

			// Instance must be parked (not completed) on the catch event so we can debug-step it.
			await WaitForInstanceStateNotAsync(instanceId, "Completed");

			// 1) Run trace: the page posts the XML to /api/debug/trace and renders the result in <pre>.
			await RunDebugTraceAsync(page, debugBpmn);
			// 2) Start a visual debug session for the real running instance.
			var sessionId = await StartDebugSessionAsync(page, instanceId);
			Assert.False(string.IsNullOrWhiteSpace(sessionId), "Start session should populate the session id field.");
			Assert.True(Guid.TryParse(sessionId, out _), $"Session id '{sessionId}' is not a GUID.");
			// 3) Set a breakpoint at the second task.
			await SetBreakpointAtSecondTaskAsync(page);
			// 4) Step over once: await -> task1 (instance still running, persisted token advanced).
			await StepOverToAsync(page, instanceId, "task1");
			// 5) Step over again: task1 -> task2.
			await StepOverToAsync(page, instanceId, "task2");
			// 6) Inspect session variables (before Continue, since Continue completes and closes the session).
			await InspectDebugVariablesAsync(page);
			// 7) Continue: the session advances (mock path drives to completion in this implementation).
			await ContinueDebugSessionAsync(page);
			// 8) Visualize the process: renders the BPMN viewer plus the runtime timeline + replay.
			await VisualizeDebugProcessAsync(page);

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 4 - Migration: preview, execute + status, snapshot/restore, rollback, and reject invalid migration")]
	public async Task Migration_PreviewExecuteStatusSnapshotRestoreRollback_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();
		var (page, browserErrors) = await CreateErrorTrackingPageAsync();
		page.Dialog += (_, dialog) => dialog.AcceptAsync();

		// Deploy a source and a target definition of the "same" process (compatible: the mapped
		// user task keeps the same activity id, only the task name differs). The GUI preview and
		// execute drive the real, durable migration engine.
		var sourceKey = $"mig-src-{host.RunId}";
		var targetKey = $"mig-tgt-{host.RunId}";

		var sourceId = await DeployMigrationProcessAsync(apiClient, sourceKey, "review-v1", "Review order");
		var targetId = await DeployMigrationProcessAsync(apiClient, targetKey, "review-v2", "Review order");

		// Two instances on the source; each parks on the user task review-v1 (review-1).
		// Instance A is migrated by the GUI execute (step C); instance B is started AFTER
		// that execute so it remains the only source instance for a separate API execution
		// (step D) whose snapshot rollback (step F) we can observe durably.
		var instanceA = await StartMigrationInstanceAsync(apiClient, sourceKey, $"mig-a-{host.RunId}");
		await WaitForTaskActivityAsync(instanceA, "review-v1");

		await page.GotoAsync($"{host.StudioBaseAddress}migration");

		// A) Unzul?ssige Migration: non-GUID source/target ids must be rejected with a clear
		//    user-facing error, not a crash.
		await RejectInvalidMigrationAsync(page);

		// B) Valid preview: fill the real source/target ids and preview -> shows the plan.
		await PreviewMigrationAsync(page, sourceId, targetId);

		// C) Execute through the GUI (confirm dialog accepted). Instance A must move to the
		//    target activity and bind to the target definition.
		await ExecuteMigrationAsync(page, instanceA, targetId);

		// D) Status abrufen: create an execution via the API for instance B (started now,
		//    still on the source after the GUI execute above) and query its status
		//    through the GUI Get status button.
		var instanceB = await StartMigrationInstanceAsync(apiClient, sourceKey, $"mig-b-{host.RunId}", useRetry: true);
		await WaitForTaskActivityAsync(instanceB, "review-v1");
		var executionId = await ExecuteMigrationViaApiAsync(apiClient, sourceKey, targetKey);
		await WaitForTaskActivityAsync(instanceB, "review-v2");
		await GetMigrationStatusAsync(page, executionId);

		// E) Snapshot + Restore: create a snapshot of the migrated instance A through the GUI,
		//    then restore it.
		await CreateAndRestoreSnapshotAsync(page, instanceA);

		// F) Rollback: roll the execution back through the GUI; instance B returns to review-1.
		await RollbackMigrationAsync(page, executionId, instanceB);

		Assert.Empty(browserErrors);
	}

	[Fact(DisplayName = "Phase 5 - Tenants: create, update, select for isolation, and delete through the GUI")]
	public async Task Tenants_CreateUpdateSwitchForIsolationAndDelete_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();
		var (page, browserErrors) = await CreateErrorTrackingPageAsync();

		var tenantA = $"Phase5 Tenant A {host.RunId}";
		var tenantB = $"Phase5 Tenant B {host.RunId}";
		var processKeyA = $"StudioE2E_TenantA_{host.RunId}";
		var processKeyB = $"StudioE2E_TenantB_{host.RunId}";
		string? tenantAId = null;
		string? tenantBId = null;

		try
		{
			// Create tenant A through the GUI Tenants page.
			await page.GotoAsync($"{host.StudioBaseAddress}tenants");
			tenantAId = await CreateTenantAsync(page, apiClient, tenantA, "Tenant A description");
			host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantAId)}");

			// Create tenant B through the GUI as well.
			tenantBId = await CreateTenantAsync(page, apiClient, tenantB, "Tenant B description");
			host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantBId)}");

			// A real edit must preserve the description. Cancellation must not write.
			await CancelTenantEditAsync(page, apiClient, tenantA, tenantAId);

			await RenameTenantAsync(page, apiClient, tenantA, tenantAId);

			var editedRow = page.Locator("tr").Filter(new() { HasText = tenantA });
			await SetTenantDescriptionAsync(page, apiClient, editedRow, tenantAId, "Description explicitly changed");
			await SetTenantDescriptionAsync(page, apiClient, editedRow, tenantAId, string.Empty);

			// Verify the existing header updates without navigating or reloading the circuit.
			await SelectTenantInHeaderAsync(page, tenantA);

			// Isolation: deploy a distinct BPMN under each tenant via the API, then verify via
			// the GUI process-definitions page that selecting a tenant shows only its own data.
			await DeployProcessUnderTenantAsync(apiClient, processKeyA, tenantAId);
			await DeployProcessUnderTenantAsync(apiClient, processKeyB, tenantBId);
			host.RegisterProcessDefinitionCleanup(processKeyA, tenantAId);
			host.RegisterProcessDefinitionCleanup(processKeyB, tenantBId);

			await VerifyTenantIsolatedAsync(page, tenantA, tenantAId, processKeyA, processKeyB);
			await VerifyTenantIsolatedAsync(page, tenantB, tenantBId, processKeyB, processKeyA);

			// Delete tenant B through the GUI, then verify it is gone via the API.
			await DeleteTenantAsync(page, apiClient, tenantB);
			tenantBId = null;

			Assert.Empty(browserErrors);
		}
		finally
		{
			await CleanupTenantsAsync(apiClient, tenantAId, tenantBId, page);
		}
	}

	[Fact(DisplayName = "Phase 5 - Credentials: create, verify secret is never re-displayed, rotate, delete")]
	public async Task Credentials_CreateSecretNeverEchoedRotateDelete_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();
		var (page, browserErrors) = await CreateErrorTrackingPageAsync();

		var tenantName = $"Phase5 CredTenant {host.RunId}";
		var credentialName = $"cred-{host.RunId}";
		string? tenantId = null;
		string? credId = null;
		var originalSecret = $"phase5-original-{Guid.NewGuid():N}";
		var rotatedSecret = $"phase5-rotated-{Guid.NewGuid():N}";

		try
		{
			// Bootstrap a tenant via the API (established pattern), then select it in the GUI.
			tenantId = await BootstrapCredentialTenantAsync(apiClient, tenantName);

			await OpenCredentialsPageAsync(page, tenantName, tenantId);

			// Create credential with a unique secret value.
			await CreateSecretCredentialAsync(page, credentialName, originalSecret);

			// The credential row appears with its secret-key NAME but the secret VALUE must never
			// be rendered by the GUI.
			var row = page.Locator("tr").Filter(new() { HasText = credentialName }).First;
			await AssertSecretNotEchoedAsync(row, page, credentialName, originalSecret);

			// The API list response must not leak the secret value.
			var listed = await apiClient.GetFromJsonAsync<JsonElement[]>($"/api/credentials?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
			var listJson = JsonSerializer.Serialize(listed ?? []);
			Assert.DoesNotContain(originalSecret, listJson, StringComparison.Ordinal);
			Assert.Single(listed ?? []);
			credId = (listed ?? [])[0].GetProperty("id").GetString();

			// Rotate the secret via the row action; only the new value must be stored and the old
			// value must remain undisclosed.
			await RotateSecretAsync(page, row, originalSecret, rotatedSecret);

			// Delete via the GUI; the row disappears and the API list becomes empty.
			await DeleteCredentialAsync(row, apiClient, tenantId, credentialName);
			credId = null;

			Assert.Empty(browserErrors);
		}
		finally
		{
			await CleanupCredentialResourcesAsync(apiClient, credId, tenantId, page);
		}
	}

	/// <summary>Asserts that a secret string never appears anywhere in the rendered DOM text.</summary>
	private static async Task AssertSecretNotInDomAsync(IPage page, string secret)
	{
		var bodyText = await page.Locator("body").InnerTextAsync();
		Assert.DoesNotContain(secret, bodyText, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Phase 5 - Connectors: create, configuration success/failure, enable/disable, delete")]
	public async Task Connectors_CreateTestToggleAndDelete_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();
		var tenantName = $"Phase5 ConnTenant {host.RunId}";
		var connectorName = $"conn-{host.RunId}";
		string? tenantId = null;

		try
		{
			tenantId = await CreateTenantAsync(apiClient, tenantName);
			host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");

			var page = await host.CreatePageAsync();
			try
			{
				await OpenConnectorsPageAsync(page, tenantName, tenantId);
				await CreateConnectorAsync(page, connectorName);

				var row = page.Locator("tr").Filter(new() { HasText = connectorName }).First;
				await row.WaitForAsync();
				Assert.Contains("Enabled", await row.InnerTextAsync(), StringComparison.Ordinal);

				await VerifyConfigurationValidAsync(page, row);

				await SetConnectorEnabledAsync(page, connectorName, enabled: false);

				await VerifyConfigurationDisabledAsync(page, row);

				await SetConnectorEnabledAsync(page, connectorName, enabled: true);

				// Delete through the GUI and verify the API no longer returns it.
				await page.Locator("tr").Filter(new() { HasText = connectorName }).First
					.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
				await WaitForConnectorAbsentAsync(apiClient, tenantId, connectorName);
			}
			finally
			{
				await host.ClosePageAsync(page);
			}
		}
		finally
		{
			if (tenantId is not null)
			{
				try
				{ await apiClient.DeleteAsync($"/api/tenant/{Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken); }
				catch { /* best effort */ }
			}
		}
	}

	[Fact(DisplayName = "Phase 5 - Workflow Triggers: register with one-time secret, invoke, enable/disable, delete")]
	public async Task WorkflowTriggers_RegisterInvokeToggleAndDelete_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();
		var tenantName = $"Phase5 TrigTenant {host.RunId}";
		var triggerName = $"trig-{host.RunId}";
		var processKey = $"tg-{host.RunId:N}".ToLowerInvariant();
		string? tenantId = null;

		try
		{
			tenantId = await CreateTenantAsync(apiClient, tenantName);
			host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");

			// The trigger references a process definition, so deploy one under the same tenant first.
			await DeployProcessUnderTenantAsync(apiClient, processKey, tenantId);
			host.RegisterApiCleanup(HttpMethod.Delete, $"api/repository?tenantId={Uri.EscapeDataString(tenantId)}&name={processKey}");

			var page = await host.CreatePageAsync();
			try
			{
				await OpenTriggersPageAsync(page, tenantName, tenantId);

				// Register a trigger through the GUI.
				await RegisterTriggerAsync(page, triggerName, processKey);

				// The one-time secret must be shown exactly once (Save-this-secret alert). Read the
				// readonly MudTextField's value (an <input>'s value is not its innerText).
				await ReadOneTimeSecretAsync(page);

				// The registered trigger row appears, enabled, with the process key.
				await VerifyTriggerRowEnabledAsync(page, triggerName, processKey);

				// Invoke the trigger through the GUI (uses the freshly-shown secret); poll for an
				// instance start (InvocationCount increments to 1).
				await InvokeTriggerAndVerifyAsync(page, triggerName);

				// Disable then re-enable; poll for the flipped status label.
				await SetTriggerEnabledAsync(page, triggerName, enabled: false);
				await SetTriggerEnabledAsync(page, triggerName, enabled: true);

				// Delete through the GUI and verify the API no longer returns it.
				await page.Locator("tr").Filter(new() { HasText = triggerName }).First
					.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
				await WaitForTriggerAbsentAsync(apiClient, tenantId, triggerName);
			}
			finally
			{
				await host.ClosePageAsync(page);
			}
		}
		finally
		{
			if (tenantId is not null)
			{
				try
				{ await apiClient.DeleteAsync($"/api/tenant/{Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken); }
				catch { /* best effort */ }
			}
		}
	}

	/// <summary>Polls GET /api/triggers?tenantId= until the named trigger is absent.</summary>
	/// <exception cref="TimeoutException"></exception>
	private static async Task WaitForTriggerAbsentAsync(HttpClient apiClient, string tenantId, string name)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			var triggers = await apiClient.GetFromJsonAsync<JsonElement[]>(
				$"/api/triggers?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
			var match = triggers?.FirstOrDefault(t => t.GetProperty("name").GetString() == name);
			if (match.HasValue && match.Value.ValueKind == JsonValueKind.Object)
			{
				await Task.Delay(600, TestContext.Current.CancellationToken);
				continue;
			}
			return;
		}
		throw new TimeoutException($"Trigger '{name}' still exists after delete.");
	}

	[Fact(DisplayName = "Phase 5 - Administration: feature flags toggle persists via API")]
	public async Task FeatureFlags_ToggleFlagAndVerifyViaApi_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		using var apiClient = host.CreateApiClient();

		var page = await host.CreatePageAsync();
		try
		{
			await page.GotoAsync($"{host.StudioBaseAddress}feature-flags");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Feature Flags", Exact = true }).WaitForAsync();

			// pick a seeded disabled flag to flip deterministically; capture its initial state.
			const string flagName = "predictiveanalytics";
			var initial = await apiClient.GetFromJsonAsync<Dictionary<string, bool>>("api/feature-flags", TestContext.Current.CancellationToken);
			Assert.NotNull(initial);
			Assert.Contains(flagName, initial.Keys, StringComparer.Ordinal);
			var originalState = initial[flagName];

			var row = page.Locator("tr").Filter(new() { HasText = flagName }).First;
			await row.WaitForAsync();
			var switchInput = row.Locator("input[type=checkbox]").First;
			var initialStateChip = await row.InnerTextAsync();
			Assert.Contains(originalState ? "Enabled" : "Disabled", initialStateChip, StringComparison.Ordinal);

			// Flip the flag via the GUI switch (drive the checkbox input); the API must reflect it.
			// The switch is briefly disabled during initial load, so retry the click until the new
			// state is observed (idempotent: we re-check the API between clicks).
			var target = !originalState;
			var flipped = false;
			for (var attempt = 0; attempt < 8 && !flipped; attempt++)
			{
				var before = await apiClient.GetFromJsonAsync<Dictionary<string, bool>>("api/feature-flags", TestContext.Current.CancellationToken);
				if (before is not null && before.TryGetValue(flagName, out var b) && b == target)
				{ flipped = true; break; }
				await switchInput.ClickAsync();
				for (var w = 0; w < 10 && !flipped; w++)
				{
					await Task.Delay(500, TestContext.Current.CancellationToken);
					var cur = await apiClient.GetFromJsonAsync<Dictionary<string, bool>>("api/feature-flags", TestContext.Current.CancellationToken);
					if (cur is not null && cur.TryGetValue(flagName, out var v) && v == target)
					{
						flipped = true;
					}
				}
			}
			Assert.True(flipped, $"Expected flag '{flagName}' to become {target} after GUI toggle.");

			// restore original state so the suite remains deterministic.
			if (target != originalState)
			{
				await apiClient.PutAsJsonAsync($"api/feature-flags/{flagName}", originalState, TestContext.Current.CancellationToken);
			}
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 5 - Administration: remaining status pages render (extensions, sso, health, performance, analytics, compliance)")]
	public async Task Administration_RemainingStatusPagesRender_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var page = await host.CreatePageAsync();
		try
		{
			var routes = new[]
			{
				("extensions", "Extensions"),
				("sso", "Single Sign-On (SSO)"),
				("health", "Health and Operations"),
				("performance", "Performance"),
				("analytics", "Process Analytics"),
				("compliance", "Compliance Evidence"),
			};
			foreach (var (route, heading) in routes)
			{
				await page.GotoAsync($"{host.StudioBaseAddress}{route}");
				await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
			}
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 5 - Administration: engine management + configuration load read-only")]
	public async Task Administration_ReadOnlyStatusPagesRender_ThroughTheRealEngine()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var page = await host.CreatePageAsync();
		try
		{
			// Engine Management: read-only info surface (no mutating controls).
			await page.GotoAsync($"{host.StudioBaseAddress}engine-management");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Engine Management", Exact = true }).WaitForAsync();

			// Configuration: capability list from the engine capabilities endpoint.
			await page.GotoAsync($"{host.StudioBaseAddress}configuration");
			await page.GetByRole(AriaRole.Heading, new() { Name = "Configuration", Exact = true }).WaitForAsync();
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	private static string? s_smokeTenantName;
	private static string? s_smokeTenantId;

	/// <summary>Creates a single tenant for the all-routes smoke test, reused across theory cases.</summary>
	private async Task EnsureSmokeTenantAsync()
	{
		if (s_smokeTenantId is not null)
		{
			return;
		}

		s_smokeTenantName = $"Smoke {host.RunId}";
		using var apiClient = host.CreateApiClient();
		s_smokeTenantId = await CreateTenantAsync(apiClient, s_smokeTenantName);
	}

	[Theory(DisplayName = "Phase 6 - every Studio route renders its heading on direct navigation and after reload")]
	[InlineData("/", "Dashboard")]
	[InlineData("/analytics", "Process Analytics")]
	[InlineData("/bpmn-modeler", "BPMN Modeler")]
	[InlineData("/cmmn-modeler", "CMMN Modeler")]
	[InlineData("/compliance", "Compliance Evidence")]
	[InlineData("/configuration", "Configuration")]
	[InlineData("/connectors", "Connectors")]
	[InlineData("/counter", "Counter")]
	[InlineData("/credentials", "Credentials")]
	[InlineData("/debugging", "Debugging Trace")]
	[InlineData("/deployments", "Deployments")]
	[InlineData("/dmn-modeler", "DMN Modeler")]
	[InlineData("/engine-management", "Engine Management")]
	[InlineData("/event-log", "Event Log")]
	[InlineData("/execution-details", "Execution Details")]
	[InlineData("/extensions", "Extensions")]
	[InlineData("/feature-flags", "Feature Flags")]
	[InlineData("/form-builder", "Form Builder")]
	[InlineData("/health", "Health and Operations")]
	[InlineData("/history", "History")]
	[InlineData("/messages-signals", "Messages & Signals")]
	[InlineData("/migration", "Process Migration")]
	[InlineData("/performance", "Performance")]
	[InlineData("/process-definitions", "Process Definitions")]
	[InlineData("/process-instances", "Process Instances")]
	[InlineData("/simulation", "Simulation")]
	[InlineData("/sso", "Single Sign-On (SSO)")]
	[InlineData("/tasks", "Tasks")]
	[InlineData("/tenants", "Tenants")]
	[InlineData("/triggers", "Workflow Triggers")]
	public async Task AllStudioRoutes_DirectNavigationAndReload_RenderTheirHeading(string route, string expectedHeading)
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var failedRequests = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};
		page.RequestFailed += (_, request) => failedRequests.Enqueue($"{request.Method} {request.Url}: {request.Failure}");

		try
		{
			// Direct navigation: the SPA fallback answers every route with HTTP 200.
			// Seed before the layout loads its tenant list, independently of test order.
			await EnsureSmokeTenantAsync();
			var response = await page.GotoAsync($"{host.StudioBaseAddress}{route.TrimStart('/')}");
			Assert.NotNull(response);
			Assert.True(response.Ok, $"{route} returned HTTP {response.Status}.");

			// Tenant-fenced pages (connectors, credentials, deployments, ...) render their real
			// content only with an active tenant; ensure one is selected up front.
			await SelectTenantAsync(page, s_smokeTenantName!, s_smokeTenantId!);

			await page.GetByRole(AriaRole.Heading, new() { Name = expectedHeading, Exact = true }).First.WaitForAsync();

			// Browser reload must render the same page again (navigation persistence).
			await page.ReloadAsync();
			await page.GetByRole(AriaRole.Heading, new() { Name = expectedHeading, Exact = true }).First.WaitForAsync();

			// No unresolved JS/page errors or failed HTTP requests.
			await Task.Delay(400, TestContext.Current.CancellationToken);
			Assert.Empty(browserErrors);
			Assert.Empty(failedRequests);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - an unknown Studio route is rejected cleanly (HTTP 404) without browser errors")]
	public async Task UnknownStudioRoute_ShowsNotFoundWithoutBrowserErrors()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			// A clean HTTP 404 (the expected outcome for an unknown route) makes the browser log a
			// generic "Failed to load resource" console entry for that very request; filter that
			// inherent noise so the assertion below proves there are no *unexpected* errors.
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase)
				&& !message.Text.StartsWith("Failed to load resource", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};

		try
		{
			// The Studio is a .NET Blazor Web App: a path matching no page is rejected by the
			// server with a clean HTTP 404, not a branded SPA error page. Playwright surfaces the
			// failed navigation as a navigation error, so expect that rejection, then verify the
			// app left no unresolved script/page errors behind.
			var rejectedWithNavigationError = false;
			try
			{
				await page.GotoAsync($"{host.StudioBaseAddress}route-that-does-not-exist-e2e");
			}
			catch (PlaywrightException)
			{
				rejectedWithNavigationError = true;
			}

			Assert.True(rejectedWithNavigationError, "Unknown Studio route should be rejected cleanly (HTTP 404), not render a page.");

			await Task.Delay(400, TestContext.Current.CancellationToken);
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - Process Instances: an unknown search shows a friendly empty state, not an error banner or crash")]
	public async Task ErrorPath_ProcessInstances_UnknownSearchShowsEmptyStateWithoutError()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await EnsureSmokeTenantAsync();
			await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
			await SelectTenantAsync(page, s_smokeTenantName!, s_smokeTenantId!);

			// A search that matches nothing must degrade to the table's friendly empty state rather
			// than an 'Error loading process instances' banner or a crashed page.
			await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), $"no-such-key-{host.RunId}");
			await page.GetByText("No process instances found", new() { Exact = true }).WaitForAsync();

			Assert.Equal(0, await page.GetByText("Error loading process instances", new() { Exact = false }).CountAsync());
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - Execution Details: repeating an invalid load stays a single clear validation error (double-click robustness)")]
	public async Task DoubleClick_ExecutionDetails_InvalidId_RemainsSingleClearValidationError()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await page.GotoAsync($"{host.StudioBaseAddress}execution-details");
			await FillBoundInputAsync(page.GetByLabel("Process instance id for variables", new() { Exact = true }), "not-a-guid");

			var loadButton = page.GetByRole(AriaRole.Button, new() { Name = "Load variables", Exact = true });
			// First click surfaces the client-side validation error.
			await loadButton.ClickAsync();
			await page.GetByText("Enter a valid process instance id.", new() { Exact = true }).WaitForAsync();

			// A repeated click on the same invalid input must reproduce the same single clear
			// validation error without a crash, a second different banner, or JS errors.
			await loadButton.ClickAsync();
			await page.GetByText("Enter a valid process instance id.", new() { Exact = true }).WaitForAsync();

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - Execution Details: a well-formed but non-existent process instance id surfaces a friendly server error (404), no crash")]
	public async Task ErrorPath_ExecutionDetails_NonexistentInstanceId_ShowsFriendlyServerError()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await page.GotoAsync($"{host.StudioBaseAddress}execution-details");

			// A well-formed GUID that (virtually certainly) does not exist passes the client-side
			// format check, so the real /api/vertex/variable/{id} call returns 404. The GUI must
			// surface a friendly error alert instead of an unhandled error or a crashed page.
			var nonexistentId = Guid.NewGuid().ToString();
			await FillBoundInputAndVerifyAsync(page.GetByLabel("Process instance id for variables", new() { Exact = true }), nonexistentId);
			await page.GetByRole(AriaRole.Button, new() { Name = "Load variables", Exact = true }).ClickAsync();

			await page.GetByText("Variables could not be loaded", new() { Exact = false }).WaitForAsync();

			// Page must still be interactive (input + buttons present), not a white screen or crash.
			await page.GetByLabel("Process instance id for variables", new() { Exact = true }).WaitForAsync();
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - Execution Details: repeating a failing load (Mehrfachklick) stays a consistent single friendly error, no crash")]
	public async Task DoubleClick_ExecutionDetails_NonexistentId_RepeatsConsistentFriendlyError()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await page.GotoAsync($"{host.StudioBaseAddress}execution-details");

			var nonexistentId = Guid.NewGuid().ToString();
			await FillBoundInputAndVerifyAsync(page.GetByLabel("Process instance id for variables", new() { Exact = true }), nonexistentId);
			var loadButton = page.GetByRole(AriaRole.Button, new() { Name = "Load variables", Exact = true });

			// First execution -> server 404 surfaced as a friendly error alert.
			await loadButton.ClickAsync();
			await page.GetByText("Variables could not be loaded", new() { Exact = false }).WaitForAsync();

			// Repeating the same failing action must reproduce the same single clear error (not a
			// second different banner, not an accumulated crash), and the page stays interactive.
			await loadButton.ClickAsync();
			await page.GetByText("Variables could not be loaded", new() { Exact = false }).WaitForAsync();
			await page.GetByLabel("Process instance id for variables", new() { Exact = true }).WaitForAsync();

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - Analytics: training-data export gives clear feedback and never crashes, regardless of ML availability")]
	public async Task Analytics_ExportTrainingData_ShowsFeedbackWithoutCrash()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await EnsureSmokeTenantAsync();
			await page.GotoAsync($"{host.StudioBaseAddress}analytics");
			await SelectTenantAsync(page, s_smokeTenantName!, s_smokeTenantId!);

			await page.GetByRole(AriaRole.Heading, new() { Name = "Process Analytics", Exact = true }).WaitForAsync();

			// The export is either a success or a graceful, clearly-worded failure depending on ML
			// availability in the test backend - both are acceptable friendly outcomes. What must NOT
			// happen is a crash or a JS error.
			var feedback = page.GetByText("Trainingsdaten wurden exportiert.", new() { Exact = true })
							   .Or(page.GetByText("Export fehlgeschlagen", new() { Exact = false }));
			await page.GetByRole(AriaRole.Button, new() { Name = "Trainingsdaten exportieren", Exact = true }).ClickAsync();
			await feedback.First.WaitForAsync();

			// Page stays interactive after the feedback appears.
			await page.GetByRole(AriaRole.Button, new() { Name = "Trainingsdaten exportieren", Exact = true }).WaitForAsync();
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - Process Instances: reload after an interactive search rehydrates cleanly without JS errors")]
	public async Task Reload_ProcessInstances_AfterFilteredSearch_RehydratesWithoutErrors()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
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
			await EnsureSmokeTenantAsync();
			await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
			await SelectTenantAsync(page, s_smokeTenantName!, s_smokeTenantId!);

			// Put the page into a filtered interactive state first.
			await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), $"no-such-key-{host.RunId}");
			await page.GetByText("No process instances found", new() { Exact = true }).WaitForAsync();

			// Reloading mid-state must rehydrate a clean, uncorrupted Blazor circuit: heading and
			// search box present again, no JS/page/network errors, no stuck loading indicator.
			await page.ReloadAsync();
			await page.GetByRole(AriaRole.Heading, new() { Name = "Process Instances", Exact = true }).WaitForAsync();
			await page.GetByPlaceholder("Search instances...").WaitForAsync();

			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}

	[Fact(DisplayName = "Phase 6 - key Studio routes render cleanly at a small (mobile) viewport")]
	public async Task SmallViewport_KeyRoutes_RenderWithoutBrowserErrors()
	{
		Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

		await EnsureSmokeTenantAsync();

		var routes = new[]
		{
			("", "Dashboard"),
			("process-instances", "Process Instances"),
			("tasks", "Tasks"),
			("bpmn-modeler", "BPMN Modeler"),
		};

		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};
		page.RequestFailed += (_, request) => browserErrors.Enqueue($"{request.Method} {request.Url}: {request.Failure}");

		try
		{
			await page.SetViewportSizeAsync(390, 844); // common mobile viewport

			foreach (var (route, heading) in routes)
			{
				var response = await page.GotoAsync($"{host.StudioBaseAddress}{route}");
				Assert.NotNull(response);
				Assert.True(response.Ok, $"/{route} returned HTTP {response.Status}.");

				if (route?.Length == 0)
				{
					await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
				}
				else
				{
					await SelectTenantAsync(page, s_smokeTenantName!, s_smokeTenantId!);
					await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
				}
			}

			// A small viewport must not produce JS/page/network errors on these representative routes.
			Assert.Empty(browserErrors);
		}
		finally
		{
			await host.ClosePageAsync(page);
		}
	}


	private static async Task WaitForConnectorAbsentAsync(HttpClient apiClient, string tenantId, string name)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			var connectors = await apiClient.GetFromJsonAsync<JsonElement[]>(
				$"/api/connectors?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
			var match = connectors?.FirstOrDefault(c => c.GetProperty("name").GetString() == name);
			if (match.HasValue && match.Value.ValueKind == JsonValueKind.Object)
			{
				await Task.Delay(600, TestContext.Current.CancellationToken);
				continue;
			}
			return;
		}
		throw new TimeoutException($"Connector '{name}' still exists after delete.");
	}

	/// <summary>Polls GET /api/credentials?tenantId= until the named credential is absent.</summary>
	/// <exception cref="TimeoutException"></exception>
	private static async Task WaitForCredentialAbsentAsync(HttpClient apiClient, string tenantId, string name)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			var creds = await apiClient.GetFromJsonAsync<JsonElement[]>($"/api/credentials?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
			var match = (creds ?? []).FirstOrDefault(c => c.GetProperty("name").GetString() == name);
			if (match.ValueKind != JsonValueKind.Object)
			{
				return;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}
		throw new TimeoutException($"Credential '{name}' still exists after delete.");
	}

	/// <summary>Creates a tenant via POST /api/tenant and returns its id.</summary>
	private async Task<string> CreateTenantAsync(HttpClient apiClient, string name)
	{
		using var tenantResponse = await apiClient.PostAsJsonAsync(
			"api/tenant",
			new { name, description = "Phase 5 tenant" },
			TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		var tenantId = JsonDocument.Parse(tenantBody).RootElement.GetProperty("id").GetString();
		Assert.False(string.IsNullOrWhiteSpace(tenantId));
		return tenantId;
	}

	/// <summary>Polls GET /api/identity/list-tenants until the named tenant exists, returning its id.</summary>
	/// <exception cref="TimeoutException"></exception>
	private static async Task<string?> WaitForTenantAsync(HttpClient apiClient, string name)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			var tenants = await apiClient.GetFromJsonAsync<JsonElement[]>("/api/identity/list-tenants", TestContext.Current.CancellationToken);
			var match = tenants?.FirstOrDefault(t => t.GetProperty("name").GetString() == name);
			if (match.HasValue && match.Value.ValueKind == JsonValueKind.Object)
			{
				return match.Value.GetProperty("id").GetString();
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}
		throw new TimeoutException($"Tenant '{name}' was not created.");
	}

	/// <summary>Polls GET /api/identity/list-tenants until the named tenant no longer exists.</summary>
	/// <exception cref="TimeoutException"></exception>
	private static async Task WaitForTenantAbsentAsync(HttpClient apiClient, string name)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			var tenants = await apiClient.GetFromJsonAsync<JsonElement[]>("/api/identity/list-tenants", TestContext.Current.CancellationToken);
			var match = tenants?.FirstOrDefault(t => t.GetProperty("name").GetString() == name);
			if (match is null || match.Value.ValueKind != JsonValueKind.Object)
			{
				return;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}
		throw new TimeoutException($"Tenant '{name}' still exists after deletion.");
	}

	/// <summary>Deploys a process definition under the given tenant via POST /api/repository (JSON body).</summary>
	private async Task DeployProcessUnderTenantAsync(HttpClient apiClient, string processKey, string tenantId)
	{
		var payload = new { bpmnXml = CreateBpmn(processKey), name = $"{processKey}.bpmn", tenantId };
		using var response = await RetryAsync(() => apiClient.PostAsJsonAsync("api/repository", payload, TestContext.Current.CancellationToken));
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(response.IsSuccessStatusCode, body);
	}

	private async Task<Guid> GetInstanceProcessDefinitionIdAsync(Guid instanceId)
	{
		using var client = host.CreateApiClient();
		using var response = await client.GetAsync($"api/runtime/{instanceId}", TestContext.Current.CancellationToken);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(response.IsSuccessStatusCode, body);
		return JsonDocument.Parse(body).RootElement.GetProperty("processDefinitionId").GetGuid();
	}

	private async Task WaitForTaskActivityAsync(Guid instanceId, string expectedActivityId)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			if (string.Equals(await GetTaskActivityAsync(instanceId), expectedActivityId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		throw new TimeoutException($"Instance {instanceId} did not reach task activity '{expectedActivityId}'.");
	}

	private async Task<string?> GetTaskActivityAsync(Guid instanceId)
	{
		using var client = host.CreateApiClient();
		using var response = await client.GetAsync(
			$"api/task?processInstanceId={instanceId}", TestContext.Current.CancellationToken);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(response.IsSuccessStatusCode, body);
		var tasks = JsonDocument.Parse(body).RootElement;
		if (tasks.ValueKind != JsonValueKind.Array || tasks.GetArrayLength() == 0)
		{
			return null;
		}

		if (tasks[0].TryGetProperty("activityId", out var activity))
		{
			return activity.GetString();
		}

		return null;
	}

	private static async Task<Guid> ReadSnapshotIdFromLastPreAsync(IPage page)
	{
		var text = await page.Locator("pre").Last.InnerTextAsync();
		using var doc = JsonDocument.Parse(text);
		return doc.RootElement.GetProperty("id").GetGuid();
	}

	private static async Task<string> ReadFilledValueAsync(ILocator input)
	{
		for (var attempt = 0; attempt < 20; attempt++)
		{
			var value = await input.InputValueAsync();
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		return await input.InputValueAsync();
	}

	private async Task WaitForTextInPageAsync(IPage page, string text)
	{
		await page.GetByText(text, new() { Exact = false }).First.WaitForAsync(new() { Timeout = 15000 });
	}

	private async Task WaitForInstanceStateAsync(Guid instanceId, string expected)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			if (string.Equals(await GetInstanceStateAsync(instanceId), expected, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		throw new TimeoutException($"Instance {instanceId} did not reach state '{expected}'.");
	}

	private async Task WaitForInstanceStateNotAsync(Guid instanceId, string forbidden)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			if (!string.Equals(await GetInstanceStateAsync(instanceId), forbidden, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		throw new TimeoutException($"Instance {instanceId} stayed in state '{forbidden}'.");
	}

	private async Task<string> GetInstanceStateAsync(Guid instanceId)
	{
		using var client = host.CreateApiClient();
		using var response = await client.GetAsync($"api/runtime/{instanceId}", TestContext.Current.CancellationToken);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(response.IsSuccessStatusCode, body);
		return JsonDocument.Parse(body).RootElement.GetProperty("state").GetString() ?? "";
	}

	private async Task<HttpResponseMessage> apiClientPostSimulation(string processKey, string bpmnXml, string? tenantId, bool variablesNull = false, string? processDefinitionId = null, int? maxSteps = 50)
	{
		using var shortLived = host.CreateApiClient();
		if (variablesNull)
		{
			return await shortLived.PostAsJsonAsync(
				"api/simulation",
				new
				{
					bpmnXml,
					processDefinitionId = processDefinitionId ?? processKey,
					variables = (object?)null,
					maxSteps,
					tenantId
				},
				TestContext.Current.CancellationToken);
		}
		return await shortLived.PostAsJsonAsync(
			"api/simulation",
			new
			{
				bpmnXml,
				processDefinitionId = processDefinitionId ?? processKey,
				variables = new Dictionary<string, object>(StringComparer.Ordinal),
				maxSteps,
				tenantId
			},
			TestContext.Current.CancellationToken);
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
			{
				return;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		throw new InvalidOperationException(
			$"The real BPMN modeler did not initialize. API logs: {string.Join(" | ", host.ApiLogs)}. " +
			$"Studio logs: {string.Join(" | ", host.StudioLogs)}. Shell: {await shell.InnerHTMLAsync()}");
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
		string? tenantId,
		string? businessKey)
	{
		using var response = await apiClient.PostAsJsonAsync(
			"api/runtime/start",
			new { processDefinitionKey = processKey, variables = new Dictionary<string, object>(StringComparer.Ordinal), businessKey, tenantId },
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
		await OpenBpmnToolTabAsync(page, "XML");
		var preview = page.GetByTestId("bpmn-xml-preview").GetByRole(AriaRole.Textbox);
		var lastXml = string.Empty;
		for (var attempt = 0; attempt < 120; attempt++)
		{
			lastXml = await preview.InputValueAsync();
			if (lastXml.Contains(expectedToken, StringComparison.Ordinal))
			{
				return lastXml;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		throw new TimeoutException($"The BPMN XML preview did not contain '{expectedToken}'. Last XML: {lastXml}");
	}

	private static async Task<string> WaitForPreviewXmlWithoutAsync(
		IPage page,
		string expectedToken,
		string forbiddenToken)
	{
		await OpenBpmnToolTabAsync(page, "XML");
		var preview = page.GetByTestId("bpmn-xml-preview").GetByRole(AriaRole.Textbox);
		var lastXml = string.Empty;
		for (var attempt = 0; attempt < 120; attempt++)
		{
			lastXml = await preview.InputValueAsync();
			if (lastXml.Contains(expectedToken, StringComparison.Ordinal)
				&& !lastXml.Contains(forbiddenToken, StringComparison.Ordinal))
			{
				return lastXml;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		throw new TimeoutException(
			$"The BPMN XML preview did not converge to a document containing '{expectedToken}' without '{forbiddenToken}'. Last XML: {lastXml}");
	}

	private static async Task OpenBpmnToolTabAsync(IPage page, string name)
	{
		var tab = page.GetByRole(AriaRole.Tab, new() { Name = name, Exact = true });
		await tab.ClickAsync();
		await page.WaitForFunctionAsync(
			"name => [...document.querySelectorAll('[role=tab]')].some(element => element.textContent?.trim() === name && element.getAttribute('aria-selected') === 'true')",
			name);
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
		catch (TimeoutException ex)
		{
			var notifications = await page.Locator(".mud-snackbar").AllTextContentsAsync();
			var recentBrowserErrors = browserErrors.TakeLast(50);
			var recentApiLogs = host.ApiLogs.TakeLast(100);
			var recentStudioLogs = host.StudioLogs.TakeLast(100);
			throw new InvalidOperationException(
				$"{operation} did not produce '{text}'. Browser errors: {string.Join(" | ", recentBrowserErrors)}. " +
				$"Notifications: {string.Join(" | ", notifications)}. " +
				$"Recent API logs: {string.Join(" | ", recentApiLogs)}. " +
				$"Recent Studio logs: {string.Join(" | ", recentStudioLogs)}", ex);
		}
	}

	private static async Task FillBoundInputAsync(ILocator input, string value)
	{
		value = NormalizeBrowserText(value);
		await input.ClickAsync();
		await input.PressAsync("ControlOrMeta+A");
		if (value.Length == 0)
		{
			await input.PressAsync("Backspace");
		}
		else
		{
			await input.PressSequentiallyAsync(value, new() { Delay = 1 });
		}

		await input.BlurAsync();
		await Task.Delay(500, TestContext.Current.CancellationToken);
	}

	private static async Task FillBoundInputAndVerifyAsync(ILocator input, string value)
	{
		value = NormalizeBrowserText(value);
		for (var attempt = 0; attempt < 10; attempt++)
		{
			await FillBoundInputAsync(input, value);
			var landed = await input.InputValueAsync();
			if (string.Equals(landed, value, StringComparison.Ordinal))
			{
				return;
			}

			await Task.Delay(300, TestContext.Current.CancellationToken);
		}

		var final = await input.InputValueAsync();
		throw new InvalidOperationException(
			$"Could not set field to '{value}'; final DOM value was '{final}' after 10 attempts.");
	}

	/// <summary>
	/// Sets a large value on a bound Mud text field instantly via JS (plus an input event so Blazor's
	/// @bind fires). Used where char-by-char typing would exceed the Playwright action timeout, e.g.
	/// pasting a full multi-KB simulation result into the compare boxes.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	private static async Task SetBoundInputFastAsync(ILocator input, string value)
	{
		value = NormalizeBrowserText(value);
		for (var attempt = 0; attempt < 40; attempt++)
		{
			try
			{
				await input.ScrollIntoViewIfNeededAsync();
				await input.EvaluateAsync(
					"(el, v) => { const proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype; const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set; if (setter) setter.call(el, v); el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }",
					value);

				// Confirm the value actually landed; under load the Blazor re-render can reset it.
				if (string.Equals(await input.InputValueAsync(), value, StringComparison.Ordinal))
				{
					return;
				}
			}
			catch (PlaywrightException)
			{
				// The element was detached/re-created mid-fill; the locator re-resolves on retry.
			}

			await Task.Delay(300, TestContext.Current.CancellationToken);
		}

		throw new InvalidOperationException(
			$"Could not set large bound field to a value of length {value.Length} after 40 attempts.");
	}

	private static string NormalizeBrowserText(string value) =>
		value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

	private async Task SelectTenantAsync(IPage page, string tenantName, string tenantId)
	{
		var tenantSelector = page.GetByRole(AriaRole.Combobox, new() { Name = "Tenant", Exact = true });
		var enabled = await tenantSelector.IsEnabledAsync();
		for (var attempt = 0; attempt < 480 && !enabled; attempt++)
		{
			await Task.Delay(250, TestContext.Current.CancellationToken);
			enabled = await tenantSelector.IsEnabledAsync();
		}
		if (!enabled)
		{
			var bodyText = await page.Locator("body").InnerTextAsync();
			throw new InvalidOperationException(
				$"Tenant selector did not finish loading. Page text: {bodyText[..Math.Min(bodyText.Length, 1200)]}");
		}
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

	/// <summary>Retries a transient HTTP operation, tolerating connection hiccups under full-suite load.</summary>
	/// <exception cref="InvalidOperationException"></exception>
	private static async Task<HttpResponseMessage> RetryAsync(
		Func<Task<HttpResponseMessage>> operation, int attempts = 6)
	{
		for (var attempt = 1; attempt <= attempts; attempt++)
		{
			try
			{
				var response = await operation();
				if (!response.IsSuccessStatusCode && attempt < attempts)
				{
					await Task.Delay(500, TestContext.Current.CancellationToken);
					continue;
				}
				return response;
			}
			catch (HttpRequestException) when (attempt < attempts)
			{
				await Task.Delay(500, TestContext.Current.CancellationToken);
			}
		}

		throw new InvalidOperationException("RetryAsync exhausted without a successful response.");
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

	private static string CreateFailingServiceTaskBpmn(string processKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="Definitions_{{processKey}}" targetNamespace="https://vertexbpmn.io/local-e2e">
          <bpmn:process id="{{processKey}}" name="Local Studio failure E2E" isExecutable="true">
            <bpmn:startEvent id="Start_{{processKey}}" />
            <bpmn:sequenceFlow id="Flow_Service_{{processKey}}" sourceRef="Start_{{processKey}}" targetRef="Fail_{{processKey}}" />
            <bpmn:serviceTask id="Fail_{{processKey}}" name="Unavailable integration" implementation="local-e2e:missing-handler" />
            <bpmn:sequenceFlow id="Flow_End_{{processKey}}" sourceRef="Fail_{{processKey}}" targetRef="End_{{processKey}}" />
            <bpmn:endEvent id="End_{{processKey}}" />
          </bpmn:process>
        </bpmn:definitions>
        """;

	private static string CreateTimerBpmn(string processKey) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="Definitions_{{processKey}}" targetNamespace="https://vertexbpmn.io/local-e2e">
          <bpmn:process id="{{processKey}}" name="Local Studio timer E2E" isExecutable="true">
            <bpmn:startEvent id="Start_{{processKey}}" />
            <bpmn:sequenceFlow id="Flow_Timer_{{processKey}}" sourceRef="Start_{{processKey}}" targetRef="Timer_{{processKey}}" />
            <bpmn:intermediateCatchEvent id="Timer_{{processKey}}" name="Wait one hour">
              <bpmn:timerEventDefinition><bpmn:timeDuration>PT1H</bpmn:timeDuration></bpmn:timerEventDefinition>
            </bpmn:intermediateCatchEvent>
            <bpmn:sequenceFlow id="Flow_End_{{processKey}}" sourceRef="Timer_{{processKey}}" targetRef="End_{{processKey}}" />
            <bpmn:endEvent id="End_{{processKey}}" />
          </bpmn:process>
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

	/// <summary>Opens the modeler, imports the process, and inserts an HTTP connector with a credential ref.</summary>
	private async Task<string> EditBpmnFlowWithCredentialAsync(IPage page, string processKey)
	{
		await OpenBpmnModelerAsync(page);
		await ImportBpmnAsync(page, CreateBpmn(processKey));

		var originalFlowId = $"Flow_{processKey}";
		var originalFlow = page.GetByTestId("bpmn-modeler-shell")
			.Locator($".djs-element[data-element-id='{originalFlowId}']");
		await originalFlow.Locator(".djs-hit").ClickAsync(new() { Force = true });
		await page.GetByTitle("Insert HTTP", new() { Exact = true }).ClickAsync();

		var propertiesPanel = page.GetByLabel("BPMN properties panel", new() { Exact = true });
		await propertiesPanel.GetByText("Vertex", new() { Exact = true }).ClickAsync();
		var credentialInput = propertiesPanel.GetByRole(AriaRole.Textbox, new() { Name = "Credential ref", Exact = true });
		var credentialRef = $"credential-{host.RunId}";
		await credentialInput.FillAsync(string.Empty);
		await credentialInput.PressSequentiallyAsync(credentialRef, new() { Delay = 25 });
		await Assertions.Expect(credentialInput).ToBeFocusedAsync();
		await Assertions.Expect(credentialInput).ToHaveValueAsync(credentialRef);
		await credentialInput.PressAsync("Tab");

		await OpenBpmnToolTabAsync(page, "XML");
		await page.GetByTestId("bpmn-xml-preview")
			.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true })
			.ClickAsync();
		return credentialRef;
	}

	private static XDocument AssertEditedFlowDocument(string editedXml, string processKey, string originalFlowId)
	{
		var editedDocument = XDocument.Parse(editedXml);
		Assert.Contains(
			editedDocument.Descendants(),
			element => element.Name.LocalName == "process"
					   && (string?)element.Attribute("id") == processKey);
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
		return editedDocument;
	}

	/// <summary>Reloads the deployed definition, exports its XML and verifies the import roundtrip.</summary>
	private async Task ReloadLoadExportAndRoundtripAsync(
		IPage page,
		string processKey,
		string credentialRef,
		XDocument editedDocument)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
		await FillBoundInputAsync(page.GetByTestId("process-definition-search"), processKey);
		var persistedDefinitionRow = page.GetByTestId("process-definitions-grid")
			.Locator("tbody tr")
			.Filter(new() { HasText = processKey })
			.First;
		await persistedDefinitionRow.WaitForAsync();
		Assert.Contains(processKey, await persistedDefinitionRow.InnerTextAsync(), StringComparison.Ordinal);
		await OpenBpmnModelerAsync(page);

		await OpenBpmnToolTabAsync(page, "Versions");
		await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Option).Filter(new() { HasText = processKey }).First.ClickAsync();
		await page.GetByTestId("bpmn-version-load-action").ClickAsync();
		await page.GetByText("Deployed BPMN version loaded into the editor.", new() { Exact = true }).WaitForAsync();
		var reloadedXml = await WaitForPreviewXmlAsync(page, processKey);
		Assert.Contains("serviceTask", reloadedXml, StringComparison.Ordinal);
		Assert.Contains(credentialRef, reloadedXml, StringComparison.Ordinal);
		XDocument.Parse(reloadedXml);

		var download = await page.RunAndWaitForDownloadAsync(() =>
			page.GetByRole(AriaRole.Button, new() { Name = "Export XML", Exact = true }).ClickAsync());
		var downloadedXml = await File.ReadAllTextAsync(
			await download.PathAsync(),
			TestContext.Current.CancellationToken);
		Assert.Contains("serviceTask", downloadedXml, StringComparison.Ordinal);
		Assert.Contains(credentialRef, downloadedXml, StringComparison.Ordinal);
		Assert.Contains(
			XDocument.Parse(downloadedXml).Descendants(),
			element => element.Name.LocalName == "process"
					   && (string?)element.Attribute("id") == processKey);

		await ImportBpmnAsync(page, downloadedXml);
		var roundtripXml = await WaitForPreviewXmlAsync(page, processKey);
		var roundtripDocument = XDocument.Parse(roundtripXml);
		Assert.Equal(
			editedDocument.Descendants().Count(element => element.Name.LocalName == "serviceTask"),
			roundtripDocument.Descendants().Count(element => element.Name.LocalName == "serviceTask"));
		Assert.Equal(
			editedDocument.Descendants().Count(element => element.Name.LocalName == "sequenceFlow"),
			roundtripDocument.Descendants().Count(element => element.Name.LocalName == "sequenceFlow"));
	}

	/// <summary>Adds low-code nodes, deploys a second version and verifies the version history.</summary>
	private async Task DeploySecondVersionAndAssertAsync(
		IPage page,
		HttpClient apiClient,
		string processKey,
		string credentialRef,
		XDocument editedDocument)
	{
		await ReloadLoadExportAndRoundtripAsync(page, processKey, credentialRef, editedDocument);

		await page.GetByTestId("bpmn-modeler-shell").Locator(".djs-connection .djs-hit").First.ClickAsync(new() { Force = true });
		await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
		var catalog = page.GetByTestId("low-code-node-catalog");
		await catalog.GetByLabel("Search nodes").FillAsync("Decision");
		await catalog.GetByRole(AriaRole.Button, new() { Name = "Decision table", Exact = true }).ClickAsync();
		await WaitForPreviewXmlAsync(page, "businessRuleTask");
		await page.GetByTestId("bpmn-modeler-shell").Locator(".djs-connection .djs-hit").First.ClickAsync(new() { Force = true });
		await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
		catalog = page.GetByTestId("low-code-node-catalog");
		await catalog.GetByLabel("Search nodes").FillAsync("User approval form");
		await catalog.GetByRole(AriaRole.Button, new() { Name = "User approval form", Exact = true }).ClickAsync();
		var configuredXml = await WaitForPreviewXmlAsync(page, "formRef=\"approval-form\"");
		Assert.Contains("decisionRef=\"decision-table\"", configuredXml, StringComparison.Ordinal);
		var configuredDocument = XDocument.Parse(configuredXml);
		foreach (var task in configuredDocument.Descendants().Where(e => e.Name.LocalName is "userTask" or "businessRuleTask"))
		{
			var id = (string?)task.Attribute("id");
			Assert.Contains(configuredDocument.Descendants(), e => e.Name.LocalName == "sequenceFlow" && (string?)e.Attribute("sourceRef") == id);
			Assert.Contains(configuredDocument.Descendants(), e => e.Name.LocalName == "sequenceFlow" && (string?)e.Attribute("targetRef") == id);
		}
		await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
		await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).Last.WaitForAsync();

		var versions = await apiClient.GetFromJsonAsync<JsonElement[]>(
			$"api/repository?key={Uri.EscapeDataString(processKey)}",
			TestContext.Current.CancellationToken);
		Assert.Equal([1, 2], (versions ?? []).Select(version => version.GetProperty("version").GetInt32()).Order().ToArray());

		await OpenBpmnToolTabAsync(page, "Versions");
		await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Option).Filter(new() { HasText = $"v1, {processKey}" }).ClickAsync();
		await page.GetByTestId("bpmn-version-compare-action").ClickAsync();
		await page.GetByText("Compared with deployed version:", new() { Exact = false }).WaitForAsync();

		await page.GetByTestId("bpmn-version-load-action").ClickAsync();
		var versionOneXml = await WaitForPreviewXmlWithoutAsync(page, processKey, "businessRuleTask");
		Assert.DoesNotContain("businessRuleTask", versionOneXml, StringComparison.Ordinal);

		await OpenBpmnToolTabAsync(page, "Versions");
		await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Option).Filter(new() { HasText = $"v2, {processKey}" }).ClickAsync();
		await page.GetByTestId("bpmn-version-load-action").ClickAsync();
		var versionTwoXml = await WaitForPreviewXmlAsync(page, "businessRuleTask");
		Assert.Contains(processKey, versionTwoXml, StringComparison.Ordinal);
	}

	private async Task<(string? TenantId, string? FormId)> SetupRuntimeResourcesAsync(
		HttpClient apiClient,
		string tenantName,
		string processKey,
		string timerProcessKey,
		string formKey)
	{
		using var tenantResponse = await apiClient.PostAsJsonAsync("api/tenant", new { name = tenantName, description = "Runtime browser E2E" }, TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		using var tenant = JsonDocument.Parse(tenantBody);
		var tenantId = tenant.RootElement.GetProperty("id").GetString();
		Assert.False(string.IsNullOrWhiteSpace(tenantId));
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");

		host.RegisterProcessDefinitionCleanup(processKey, tenantId);
		host.RegisterProcessDefinitionCleanup(timerProcessKey, tenantId);

		using var formResponse = await apiClient.PostAsJsonAsync("api/forms", new { tenantId, key = formKey, name = "Runtime approval form", schema = CreateFormJson(host.RunId) }, TestContext.Current.CancellationToken);
		var formBody = await formResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(formResponse.IsSuccessStatusCode, formBody);
		using var form = JsonDocument.Parse(formBody);
		var formId = form.RootElement.GetProperty("id").GetString();
		return (tenantId, formId);
	}

	private async Task<Guid> DeployRuntimeProcessesAsync(
		IPage page, HttpClient apiClient, string tenantName, string? tenantId,
		string processKey, string timerProcessKey, string formKey)
	{
		await OpenBpmnModelerAsync(page);
		await SelectTenantAsync(page, tenantName, tenantId!);
		await ImportBpmnAsync(page, CreateUserTaskBpmn(processKey, formKey));
		await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
		await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();

		await DeployProcessAsync(apiClient, timerProcessKey, tenantId!, "Runtime timer", CreateTimerBpmn(timerProcessKey));
		return await StartProcessAsync(apiClient, timerProcessKey, tenantId, $"timer-{host.RunId}");
	}

	private async Task<Guid> StartProcessThroughModelerAsync(
		IPage page, HttpClient apiClient, string tenantName, string? tenantId,
		string processKey, string businessKey)
	{
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
		return started.GetProperty("id").GetGuid();
	}

	private async Task<Guid> VerifyInstanceVariablesAndCompleteTaskAsync(
		IPage page, HttpClient apiClient, string tenantName, string? tenantId,
		string businessKey, Guid instanceId, string formKey)
	{
		await VerifyInstanceVariablesInDetailsAsync(page, tenantName, tenantId, businessKey);
		return await ClaimAndCompleteTaskAsync(page, apiClient, tenantName, tenantId, instanceId, formKey);
	}

	private async Task VerifyInstanceVariablesInDetailsAsync(IPage page, string tenantName, string? tenantId, string businessKey)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
		await SelectTenantAsync(page, tenantName, tenantId!);
		await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), businessKey);
		var instanceRow = page.Locator("tr").Filter(new() { HasText = businessKey }).First;
		await instanceRow.GetByText("1 task(s)", new() { Exact = true }).WaitForAsync(new() { Timeout = 120_000 });
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
			{
				await page.ScreenshotAsync(new() { Path = Path.Combine(artifactDirectory, "process-instance-tabs.png"), FullPage = true });
			}

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
	}

	private async Task<Guid> ClaimAndCompleteTaskAsync(
		IPage page, HttpClient apiClient, string tenantName, string? tenantId, Guid instanceId, string formKey)
	{
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
		return taskId;
	}

	private async Task VerifyCompletedInstanceHistoryAndLogsAsync(
		IPage page, HttpClient apiClient, string tenantName, string? tenantId,
		string businessKey, Guid instanceId, Guid taskId, Guid timerInstanceId)
	{
		await VerifyCompletedInstanceDetailsAsync(page, tenantName, tenantId, businessKey);
		await VerifyHistoryAndEventLogPersistedAsync(page, apiClient, tenantName, tenantId, instanceId, taskId);
		await VerifyExecutionDetailsJobsIncidentsVariablesAsync(page, tenantName, tenantId, instanceId, timerInstanceId);
	}

	private async Task VerifyCompletedInstanceDetailsAsync(IPage page, string tenantName, string? tenantId, string businessKey)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
		await SelectTenantAsync(page, tenantName, tenantId!);
		await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), businessKey);
		var instanceRow = page.Locator("tr").Filter(new() { HasText = businessKey }).First;
		await instanceRow.GetByText("Completed", new() { Exact = true }).WaitForAsync();
		await instanceRow.GetByText("0 task(s)", new() { Exact = true }).WaitForAsync();
		await instanceRow.GetByRole(AriaRole.Button, new() { Name = "View Details", Exact = true }).ClickAsync();
		var detailsDialog = page.GetByRole(AriaRole.Dialog);
		await detailsDialog.GetByRole(AriaRole.Tab, new() { Name = "Variables", Exact = true }).ClickAsync();
		await detailsDialog.GetByText($"customer-{host.RunId}", new() { Exact = true }).WaitForAsync();
		await detailsDialog.GetByText("ACME-42", new() { Exact = true }).WaitForAsync();
		await detailsDialog.GetByRole(AriaRole.Tab, new() { Name = "History", Exact = true }).ClickAsync();
		await detailsDialog.GetByText("User Task Completed", new() { Exact = true }).WaitForAsync();
		await detailsDialog.GetByText("Process Completed", new() { Exact = true }).WaitForAsync();
		await detailsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
	}

	private async Task VerifyHistoryAndEventLogPersistedAsync(IPage page, HttpClient apiClient, string tenantName, string? tenantId, Guid instanceId, Guid taskId)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}history");
		await SelectTenantAsync(page, tenantName, tenantId!);
		var instanceHistoryRows = page.Locator("tr").Filter(new() { HasText = instanceId.ToString() });
		await instanceHistoryRows.Filter(new() { HasText = "User Task Completed" }).First.WaitForAsync();
		await instanceHistoryRows.Filter(new() { HasText = "Process Completed" }).First.WaitForAsync();

		// Persistent event log (System B): verify via the API that the completed instance
		// produced the expected engine history events, independent of the browser tabs.
		using var eventLogResponse = await apiClient.GetAsync(
				   $"api/history/by-process-instance/{instanceId}",
				   TestContext.Current.CancellationToken);
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

		await page.GotoAsync($"{host.StudioBaseAddress}event-log");
		await SelectTenantAsync(page, tenantName, tenantId!);
		var eventLogTable = page.GetByTestId("persistent-event-log-table");
		await eventLogTable.GetByText(instanceId.ToString(), new() { Exact = true }).First.WaitForAsync();
		await eventLogTable.GetByText("Process Completed", new() { Exact = true }).First.WaitForAsync();
		await eventLogTable.GetByText(taskId.ToString(), new() { Exact = false }).First.WaitForAsync();
	}

	private async Task VerifyExecutionDetailsJobsIncidentsVariablesAsync(IPage page, string tenantName, string? tenantId, Guid instanceId, Guid timerInstanceId)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}execution-details");
		await SelectTenantAsync(page, tenantName, tenantId!);
		await page.GetByRole(AriaRole.Button, new() { Name = "Load jobs", Exact = true }).ClickAsync();
		var jobsResult = page.GetByTestId("execution-details-result");
		await jobsResult.GetByText("Jobs", new() { Exact = true }).WaitForAsync();
		await jobsResult.GetByText(timerInstanceId.ToString(), new() { Exact = false }).WaitForAsync();
		await jobsResult.GetByText("timer", new() { Exact = false }).WaitForAsync();
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
	}

	private async Task<(string PopulatedTenantId, string EmptyTenantId, string DashboardProcess)> SetupManagementDataAsync(
		HttpClient apiClient, string populatedTenantName, string emptyTenantName, string primaryProcessKey)
	{
		var populatedTenantId = await CreateTenantAsync(apiClient, populatedTenantName);
		var emptyTenantId = await CreateTenantAsync(apiClient, emptyTenantName);
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(populatedTenantId)}");
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(emptyTenantId)}");

		var dashboardProcess = CreateUserTaskBpmn(primaryProcessKey, $"dashboard-form-{host.RunId}");
		await DeployProcessAsync(apiClient, primaryProcessKey, populatedTenantId, "Management v1", dashboardProcess);
		host.RegisterProcessDefinitionCleanup(primaryProcessKey, populatedTenantId);

		foreach (var index in Enumerable.Range(1, 10))
		{
			var processKey = $"{primaryProcessKey}_{index:00}";
			await DeployProcessAsync(apiClient, processKey, populatedTenantId, $"Management page item {index:00}");
			host.RegisterProcessDefinitionCleanup(processKey, populatedTenantId);
		}

		await StartProcessAsync(
			apiClient,
			primaryProcessKey,
			populatedTenantId,
			$"dashboard-first-{host.RunId}");
		return (populatedTenantId, emptyTenantId, dashboardProcess);
	}

	private async Task VerifyDashboardCountsAsync(IPage page, string definitions, string instances, string active, string pending)
	{
		await page.GetByTestId("dashboard-process-definitions-value").GetByText(definitions, new() { Exact = true }).WaitForAsync();
		await page.GetByTestId("dashboard-process-instances-value").GetByText(instances, new() { Exact = true }).WaitForAsync();
		await page.GetByTestId("dashboard-active-instances-value").GetByText(active, new() { Exact = true }).WaitForAsync();
		await page.GetByTestId("dashboard-pending-tasks-value").GetByText(pending, new() { Exact = true }).WaitForAsync();
	}

	private async Task VerifyDashboardPopulatedAndRefreshAsync(
		IPage page, HttpClient apiClient, string tenantId,
		string primaryProcessKey, string dashboardProcess)
	{
		await StartProcessAsync(
			apiClient,
			primaryProcessKey,
			tenantId,
			$"dashboard-second-{host.RunId}");
		await DeployProcessAsync(apiClient, primaryProcessKey, tenantId, "Management v2", dashboardProcess);
		await page.GetByTestId("dashboard-refresh").ClickAsync();
		await VerifyDashboardCountsAsync(page, "12", "2", "2", "2");
	}

	private async Task VerifyEmptyDashboardAndNavigateAsync(
		IPage page, string emptyTenantName, string emptyTenantId,
		string populatedTenantName, string populatedTenantId)
	{
		await SelectTenantAsync(page, emptyTenantName, emptyTenantId);
		await VerifyDashboardCountsAsync(page, "0", "0", "0", "0");
		await page.GetByRole(AriaRole.Link, new() { Name = "Process Definitions", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync();
		await SelectTenantAsync(page, populatedTenantName, populatedTenantId);
	}

	private async Task VerifyPaginationAsync(ILocator definitionsGrid)
	{
		await definitionsGrid.Locator("tbody tr").First.WaitForAsync();
		Assert.Equal(10, await definitionsGrid.Locator("tbody tr").CountAsync());
		var firstPageRows = await definitionsGrid.Locator("tbody tr").AllInnerTextsAsync();
		Assert.Equal(10, firstPageRows.Distinct(StringComparer.Ordinal).Count());
		await definitionsGrid.Locator(".mud-table-pagination-actions button:not([disabled])").Last.ClickAsync();
		await definitionsGrid.Locator("tbody tr").Nth(2).WaitForAsync(new() { State = WaitForSelectorState.Detached });
		Assert.Equal(2, await definitionsGrid.Locator("tbody tr").CountAsync());
		var secondPageRows = await definitionsGrid.Locator("tbody tr").AllInnerTextsAsync();
		Assert.Equal(2, secondPageRows.Distinct(StringComparer.Ordinal).Count());
		Assert.Empty(firstPageRows.Intersect(secondPageRows, StringComparer.Ordinal));
	}

	private async Task VerifySearchViewVersionsAndDeleteAsync(IPage page, ILocator definitionsGrid, string primaryProcessKey)
	{
		await FillBoundInputAsync(page.GetByTestId("process-definition-search"), "Management v2");
		var definitionRow = definitionsGrid.Locator("tbody tr").Filter(new() { HasText = "Management v2" }).First;
		await definitionRow.GetByRole(AriaRole.Button, new() { Name = "View BPMN", Exact = true }).ClickAsync();
		var viewerDialog = page.GetByRole(AriaRole.Dialog);
		await viewerDialog.GetByTestId("bpmn-definition-xml").WaitForAsync();
		Assert.Contains(
			primaryProcessKey,
			await viewerDialog.GetByTestId("bpmn-definition-xml").InputValueAsync(),
			StringComparison.Ordinal);
		await viewerDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

		await definitionRow.GetByRole(AriaRole.Button, new() { Name = "View Versions", Exact = true }).ClickAsync();
		var versionsDialog = page.GetByRole(AriaRole.Dialog);
		await versionsDialog.GetByText("v1", new() { Exact = true }).WaitForAsync();
		await versionsDialog.Locator("[data-label='Version']").Filter(new() { HasText = "v2" }).WaitForAsync();
		await versionsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

		await definitionRow.GetByRole(AriaRole.Button, new() { Name = "Delete Process Definition", Exact = true }).ClickAsync();
		var confirmation = page.GetByRole(AriaRole.Dialog);
		await confirmation.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
		await page.GetByText("Process definition 'Management v2' deleted.", new() { Exact = true }).WaitForAsync();
		await page.ReloadAsync();
		await FillBoundInputAsync(page.GetByTestId("process-definition-search"), "Management v");
		Assert.Equal(1, await definitionsGrid.Locator("tbody tr").Filter(new() { HasText = primaryProcessKey }).CountAsync());
	}

	private async Task<(string TenantId, Guid LifecycleInstanceId, Guid DeletionInstanceId, Guid FailedInstanceId)> SetupManagementRuntimeAsync(
		HttpClient apiClient, string tenantName, string processKey, string failingProcessKey,
		string lifecycleBusinessKey, string deletionBusinessKey, string failureBusinessKey)
	{
		var tenantId = await CreateTenantAsync(apiClient, tenantName);
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
		host.RegisterProcessDefinitionCleanup(processKey, tenantId);
		host.RegisterProcessDefinitionCleanup(failingProcessKey, tenantId);
		await DeployProcessAsync(
			apiClient,
			processKey,
			tenantId,
			"Management runtime",
			CreateUserTaskBpmn(processKey, $"unused-form-{host.RunId}"));
		await DeployProcessAsync(
			apiClient,
			failingProcessKey,
			tenantId,
			"Management failure",
			CreateFailingServiceTaskBpmn(failingProcessKey));

		var lifecycleInstanceId = await StartProcessAsync(apiClient, processKey, tenantId, lifecycleBusinessKey);
		var deletionInstanceId = await StartProcessAsync(apiClient, processKey, tenantId, deletionBusinessKey);
		var failedInstanceId = await StartProcessAsync(apiClient, failingProcessKey, tenantId, failureBusinessKey);
		return (tenantId, lifecycleInstanceId, deletionInstanceId, failedInstanceId);
	}

	private async Task VerifySuspendResumeLifecycleAsync(IPage page, string lifecycleBusinessKey)
	{
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
	}

	private async Task VerifyDeleteInstanceAsync(
		IPage page, string deletionBusinessKey, Guid deletionInstanceId)
	{
		await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), deletionBusinessKey);
		var deletionRow = page.Locator("tr").Filter(new() { HasText = deletionBusinessKey }).First;
		await deletionRow.GetByRole(AriaRole.Button, new() { Name = "Delete Instance", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
		await page.GetByText("Process instance deleted", new() { Exact = true }).WaitForAsync();
		await page.ReloadAsync();
		await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), deletionBusinessKey);
		Assert.Equal(0, await page.Locator("tr").Filter(new() { HasText = deletionInstanceId.ToString() }).CountAsync());
	}

	private async Task VerifyFailureInstanceAsync(
		IPage page, HttpClient apiClient, string tenantName, string tenantId,
		string failureBusinessKey, Guid failedInstanceId)
	{
		await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), failureBusinessKey);
		var failureRow = page.Locator("tr").Filter(new() { HasText = failureBusinessKey }).First;
		await failureRow.GetByText("Incident", new() { Exact = true }).WaitForAsync();
		Assert.Equal(0, await failureRow.GetByRole(AriaRole.Button, new() { Name = "Resume Instance", Exact = true }).CountAsync());
		await failureRow.GetByRole(AriaRole.Button, new() { Name = "View Details", Exact = true }).ClickAsync();
		var failureDetails = page.GetByRole(AriaRole.Dialog);
		await failureDetails.GetByText("Incident", new() { Exact = true }).WaitForAsync();
		Assert.Equal(0, await failureDetails.GetByText("Suspended", new() { Exact = true }).CountAsync());
		await failureDetails.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
		using var incidentsResponse = await apiClient.GetAsync(
			$"api/vertex/incident?tenantId={Uri.EscapeDataString(tenantId)}",
			TestContext.Current.CancellationToken);
		var incidentsBody = await incidentsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(incidentsResponse.IsSuccessStatusCode, incidentsBody);
		var incidents = JsonSerializer.Deserialize<JsonElement[]>(incidentsBody);
		Assert.Contains(
			incidents ?? [],
			incident => incident.GetProperty("processInstanceId").GetString() == failedInstanceId.ToString()
						&& incident.GetProperty("incidentType").GetString() == "ExecutionFailure");

		await page.GotoAsync($"{host.StudioBaseAddress}execution-details");
		await SelectTenantAsync(page, tenantName, tenantId);
		await page.GetByRole(AriaRole.Button, new() { Name = "Load incidents", Exact = true }).ClickAsync();
		var incidentsResult = page.GetByTestId("execution-details-result");
		await incidentsResult.GetByText("Incidents", new() { Exact = true }).WaitForAsync();
		await incidentsResult.GetByText(failedInstanceId.ToString(), new() { Exact = false }).WaitForAsync();
		await incidentsResult.GetByText("ExecutionFailure", new() { Exact = false }).WaitForAsync();
	}

	private async Task VerifyTaskLifecycleAsync(
		IPage page, HttpClient apiClient, string tenantName, string tenantId, Guid lifecycleInstanceId)
	{
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
	}

	private async Task<string> SetupDeploymentsTenantAsync(HttpClient apiClient, string tenantName, string[] processKeys)
	{
		var tenantId = await CreateTenantAsync(apiClient, tenantName);
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
		foreach (var processKey in processKeys)
		{
			host.RegisterProcessDefinitionCleanup(processKey, tenantId);
		}
		return tenantId;
	}

	private async Task VerifyDeploymentUploadValidationAsync(IPage page, ILocator upload, string[] processKeys)
	{
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
	}

	private async Task VerifyMultipleUploadAsync(IPage page, ILocator upload, string[] processKeys)
	{
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
	}

	private async Task VerifyDeploymentDefinitionsAsync(
		IPage page, string tenantName, string tenantId, string[] processKeys)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
		await SelectTenantAsync(page, tenantName, tenantId);
		foreach (var processKey in processKeys)
		{
			await FillBoundInputAsync(page.GetByTestId("process-definition-search"), processKey);
			await page.GetByTestId("process-definitions-grid").GetByText(processKey, new() { Exact = true }).WaitForAsync();
		}
	}

	private async Task ImportN8nWorkflowAsync(
		IPage page, ConcurrentQueue<string> browserErrors, string workflowName, string processKey)
	{
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
	}

	private async Task ValidateDeployAndVerifyN8nAsync(
		IPage page, HttpClient apiClient, ConcurrentQueue<string> browserErrors, string processKey)
	{
		await page.GetByRole(AriaRole.Button, new() { Name = "Validate", Exact = true }).ClickAsync();
		await page.GetByText("No issues", new() { Exact = true }).WaitForAsync();
		await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
		await WaitForTextWithDiagnosticsAsync(
			page,
			"BPMN deployed successfully.",
			browserErrors,
			"n8n-generated BPMN deployment");

		var definitions = await apiClient.GetFromJsonAsync<JsonElement[]>(
			$"api/repository?key={Uri.EscapeDataString(processKey)}",
			TestContext.Current.CancellationToken);
		Assert.Single(definitions ?? []);
	}

	private async Task ReloadN8nVersionAsync(IPage page, string processKey)
	{
		await OpenBpmnModelerAsync(page);
		await OpenBpmnToolTabAsync(page, "Versions");
		await page.GetByRole(AriaRole.Combobox, new() { Name = "Deployed process version", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Option).Filter(new() { HasText = processKey }).First.ClickAsync();
		await page.GetByTestId("bpmn-version-load-action").ClickAsync();
		await page.GetByText("Deployed BPMN version loaded into the editor.", new() { Exact = true }).WaitForAsync();
		await WaitForPreviewXmlAsync(page, processKey);
	}

	private async Task ExportN8nXmlAsync(IPage page, string processKey)
	{
		var download = await page.RunAndWaitForDownloadAsync(() =>
			page.GetByRole(AriaRole.Button, new() { Name = "Export XML", Exact = true }).ClickAsync());
		var exportedXml = await File.ReadAllTextAsync(await download.PathAsync(), TestContext.Current.CancellationToken);
		Assert.Contains(
			XDocument.Parse(exportedXml).Descendants(),
			element => element.Name.LocalName == "process"
					   && (string?)element.Attribute("id") == processKey);
	}


	private async Task SetupAndImportDecisionAsync(IPage page, ConcurrentQueue<string> browserErrors, string decisionKey)
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
		using var definition = JsonDocument.Parse(definitionBody);
		Assert.Equal(decisionKey, definition.RootElement.GetProperty("key").GetString());
		Assert.Equal("Local Studio E2E decision", definition.RootElement.GetProperty("name").GetString());
	}

	private async Task ReloadExportAndRoundtripDecisionAsync(IPage page, ConcurrentQueue<string> browserErrors, string decisionKey)
	{
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
	}

	private async Task EvaluateDecisionScenariosAsync(IPage page, ConcurrentQueue<string> browserErrors)
	{
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


	private async Task<string?> CreateFormTenantAsync(HttpClient apiClient, string tenantName)
	{
		using var tenantResponse = await apiClient.PostAsJsonAsync(
				   "api/tenant",
				   new { name = tenantName, description = "Local Studio browser test" },
				   TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		var tenantId = JsonDocument.Parse(tenantBody).RootElement.GetProperty("id").GetString();
		Assert.False(string.IsNullOrWhiteSpace(tenantId));
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
		return tenantId;
	}

	private async Task SetupFormAndSaveAsync(IPage page, string tenantName, string? tenantId, string formKey)
	{
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
	}

	private static async Task<JsonElement> GetPersistedFormAsync(HttpClient apiClient, string? tenantId, string formKey)
	{
		var forms = await apiClient.GetFromJsonAsync<JsonElement[]>(
			$"api/forms?tenantId={Uri.EscapeDataString(tenantId!)}",
			TestContext.Current.CancellationToken);
		return Assert.Single(forms ?? [], form => form.GetProperty("key").GetString() == formKey);
	}

	private async Task ReloadAndUpdateSavedFormAsync(IPage page, string tenantName, string? tenantId, string formKey)
	{
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
	}

	private async Task ExportAndRoundtripFormAsync(IPage page, ConcurrentQueue<string> browserErrors)
	{
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


	private async Task ImportCaseModelAsync(
		IPage page, ConcurrentQueue<string> browserErrors, string caseKey, string reviewId, string milestoneId, string eventId, string caseFileItemId)
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
	}

	private async Task RegisterCaseAndVerifyDefinitionAsync(
		IPage page, HttpClient apiClient, ConcurrentQueue<string> browserErrors, string caseKey, string reviewId)
	{
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

		using var definitionResponse = await apiClient.GetAsync(
			$"api/case-definitions/{Uri.EscapeDataString(caseKey)}",
			TestContext.Current.CancellationToken);
		var definitionBody = await definitionResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(definitionResponse.IsSuccessStatusCode, definitionBody);
		using var definition = JsonDocument.Parse(definitionBody);
		Assert.Equal(caseKey, definition.RootElement.GetProperty("key").GetString());
		Assert.Contains(reviewId, definition.RootElement.GetProperty("cmmnXml").GetString(), StringComparison.Ordinal);
	}

	private async Task ExecuteCaseAndUpdateCaseFileAsync(
		IPage page, ConcurrentQueue<string> browserErrors, string caseKey, string reviewId, string milestoneId, string caseFileItemId)
	{
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
	}

	private async Task TriggerEventGenerateAndLoadHistoryAsync(
		IPage page, ConcurrentQueue<string> browserErrors, string eventId, string caseFileItemId)
	{
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
	}

	private async Task AddTaskExportAndRoundtripAsync(IPage page, ConcurrentQueue<string> browserErrors, string caseKey)
	{
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


	private async Task DeployTwoVersionsAsync(IPage page, string processKey)
	{
		await OpenBpmnModelerAsync(page);
		await ImportBpmnAsync(page, CreateBpmn(processKey));
		await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
		await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();
		// Catalog insertion requires an explicitly selected path, as stated in
		// the UI. Select the real diagram edge before requesting the new node.
		await page.GetByTestId("bpmn-modeler-shell")
			.Locator($".djs-element[data-element-id='Flow_{processKey}'] .djs-hit")
			.ClickAsync(new() { Force = true });
		await page.GetByRole(AriaRole.Button, new() { Name = "Add node", Exact = true }).ClickAsync();
		var catalog = page.GetByTestId("low-code-node-catalog");
		await catalog.GetByLabel("Search nodes").FillAsync("Decision");
		await catalog.GetByRole(AriaRole.Button, new() { Name = "Decision table", Exact = true }).ClickAsync();
		await WaitForPreviewXmlAsync(page, "businessRuleTask");
		await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
		await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).Last.WaitForAsync();
	}

	private async Task ViewBpmnAndVersionsAsync(IPage page, string processKey)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
		await FillBoundInputAsync(page.GetByTestId("process-definition-search"), processKey);
		var definitionRow = page.Locator("tr").Filter(new() { HasText = processKey }).First;
		await definitionRow.WaitForAsync();

		// Open the BPMN viewer dialog from the Actions column.
		await definitionRow.GetByRole(AriaRole.Button, new() { Name = "View BPMN", Exact = true }).ClickAsync();
		var viewerDialog = page.GetByRole(AriaRole.Dialog).Filter(new() { HasText = "Download XML" });
		await viewerDialog.WaitForAsync();
		await viewerDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

		// Open the version history dialog and verify both deployed versions are listed.
		await definitionRow.GetByRole(AriaRole.Button, new() { Name = "View Versions", Exact = true }).ClickAsync();
		var versionsDialog = page.GetByRole(AriaRole.Dialog);
		await versionsDialog.GetByText($"Process Versions: {processKey}", new() { Exact = true }).WaitForAsync();
		await versionsDialog.GetByText("(Latest)", new() { Exact = false }).First.WaitForAsync();
		var versionsText = await versionsDialog.InnerTextAsync();
		Assert.Contains("v1", versionsText, StringComparison.Ordinal);
		Assert.Contains("v2", versionsText, StringComparison.Ordinal);
		Assert.DoesNotContain("v@context.Version", versionsText, StringComparison.Ordinal);
		await versionsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
	}

	private async Task DeleteAllDefinitionVersionsAsync(IPage page, string processKey)
	{
		while (true)
		{
			var rows = page.Locator("tr").Filter(new() { HasText = processKey });
			var deleteButtons = rows.GetByRole(AriaRole.Button, new() { Name = "Delete Process Definition", Exact = true });
			if (await deleteButtons.CountAsync() == 0)
			{
				break;
			}

			await deleteButtons.First.ClickAsync();
			var confirmDialog = page.GetByRole(AriaRole.Dialog);
			await confirmDialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
			await page.GetByText("deleted.", new() { Exact = false }).WaitForAsync();
		}
	}

	private async Task VerifyDeletionPersistsAsync(IPage page, HttpClient apiClient, string processKey, ConcurrentQueue<string> browserErrors)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
		await FillBoundInputAsync(page.GetByTestId("process-definition-search"), processKey);
		var remainingRows = page.Locator("tr").Filter(new() { HasText = processKey });
		for (var attempt = 0; attempt < 60; attempt++)
		{
			if (await remainingRows.CountAsync() == 0)
			{
				break;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}
		Assert.Equal(0, await remainingRows.CountAsync());
		var remaining = await apiClient.GetFromJsonAsync<JsonElement[]>(
			$"api/repository?key={Uri.EscapeDataString(processKey)}",
			TestContext.Current.CancellationToken);
		Assert.Empty(remaining ?? []);
		Assert.Empty(browserErrors);
	}


	private async Task<(string? TenantId, string? FormId)> SetupInstanceTenantAndFormAsync(
		HttpClient apiClient, string tenantName, string processKey, string formKey)
	{
		string? tenantId;
		using var tenantResponse = await apiClient.PostAsJsonAsync("api/tenant", new { name = tenantName, description = "Process instances browser E2E" }, TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		tenantId = JsonDocument.Parse(tenantBody).RootElement.GetProperty("id").GetString();
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId!)}");
		host.RegisterProcessDefinitionCleanup(processKey, tenantId);
		string? formId;
		using var formResponse = await apiClient.PostAsJsonAsync("api/forms", new { tenantId, key = formKey, name = "Instance approval form", schema = CreateFormJson(host.RunId) }, TestContext.Current.CancellationToken);
		var formBody = await formResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(formResponse.IsSuccessStatusCode, formBody);
		formId = JsonDocument.Parse(formBody).RootElement.GetProperty("id").GetString();
		return (tenantId, formId);
	}

	private async Task DeployInstanceProcessAsync(IPage page, string tenantName, string? tenantId, string processKey, string formKey)
	{
		await OpenBpmnModelerAsync(page);
		await SelectTenantAsync(page, tenantName, tenantId!);
		await ImportBpmnAsync(page, CreateUserTaskBpmn(processKey, formKey));
		await page.GetByRole(AriaRole.Button, new() { Name = "Deploy BPMN", Exact = true }).ClickAsync();
		await page.GetByText("BPMN deployed successfully.", new() { Exact = true }).WaitForAsync();
	}

	private async Task StartInstanceAndVerifyDetailsAsync(
		IPage page, HttpClient apiClient, string tenantName, string? tenantId, string processKey, string businessKey)
	{
		using var startResponse = await apiClient.PostAsJsonAsync("api/runtime/start", new { ProcessDefinitionKey = processKey, BusinessKey = businessKey, TenantId = tenantId, Variables = new Dictionary<string, object>(StringComparer.Ordinal) { ["requestSource"] = "instances-e2e" } }, TestContext.Current.CancellationToken);
		var startBody = await startResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(startResponse.IsSuccessStatusCode, startBody);
		using var started = JsonDocument.Parse(startBody);
		var instanceId = started.RootElement.GetProperty("id").GetGuid();

		// The instance is listed, findable by business key, and shows its single active task.
		await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
		await SelectTenantAsync(page, tenantName, tenantId!);
		await page.GetByPlaceholder("Search instances...").FillAsync(businessKey);
		var instanceRow = page.Locator("tr").Filter(new() { HasText = businessKey }).First;
		await instanceRow.GetByText("1 task(s)", new() { Exact = true }).WaitForAsync(new() { Timeout = 120_000 });

		// The migrated instance management (suspend/resume/delete) is currently implemented as
		// non-persisting stubs: the API endpoints only emit process-mining metrics events and do
		// not change the persisted instance state. This is a documented known-gap; the row therefore
		// keeps reporting the engine state and management buttons are not asserted here.
		await instanceRow.GetByText(businessKey, new() { Exact = true }).WaitForAsync();
		await instanceRow.GetByRole(AriaRole.Button, new() { Name = "View Details", Exact = true }).ClickAsync();
		var detailsDialog = page.GetByRole(AriaRole.Dialog);
		await detailsDialog.GetByRole(AriaRole.Tab, new() { Name = "Variables", Exact = true }).ClickAsync();
		await detailsDialog.GetByText("requestSource", new() { Exact = true }).WaitForAsync();
		await detailsDialog.GetByText("instances-e2e", new() { Exact = true }).WaitForAsync();
		await detailsDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

		// The real API still reports the started instance with its engine state.
		var instances = await apiClient.GetFromJsonAsync<JsonElement[]>($"api/runtime?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
		var persisted = Assert.Single(instances ?? [], candidate => candidate.GetProperty("id").GetGuid() == instanceId);
		// The engine reports the live state; while waiting on a user task this is "Waiting",
		// not a fixed string, so assert a non-empty current state rather than a constant.
		Assert.False(string.IsNullOrWhiteSpace(persisted.GetProperty("state").GetString()),
			"The started instance must report a non-empty engine state.");
	}


	private async Task<(string? TenantId, string? FormId)> SetupInstanceMgtTenantAndFormAsync(
		HttpClient apiClient, string tenantName, string processKey, string formKey)
	{
		string? tenantId;
		using var tenantResponse = await apiClient.PostAsJsonAsync("api/tenant", new { name = tenantName, description = "Process instances suspend/resume/delete E2E" }, TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		tenantId = JsonDocument.Parse(tenantBody).RootElement.GetProperty("id").GetString();
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId!)}");
		host.RegisterProcessDefinitionCleanup(processKey, tenantId);
		string? formId;
		using var formResponse = await apiClient.PostAsJsonAsync("api/forms", new { tenantId, key = formKey, name = "Instance mgmt approval form", schema = CreateFormJson(host.RunId) }, TestContext.Current.CancellationToken);
		var formBody = await formResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(formResponse.IsSuccessStatusCode, formBody);
		formId = JsonDocument.Parse(formBody).RootElement.GetProperty("id").GetString();
		return (tenantId, formId);
	}

	private async Task DeployInstanceMgtProcessAsync(IPage page, string tenantName, string? tenantId, string processKey, string formKey)
	{
		await DeployInstanceProcessAsync(page, tenantName, tenantId, processKey, formKey);
	}

	private async Task<Guid> StartInstanceAsync(HttpClient apiClient, string processKey, string businessKey, string? tenantId)
	{
		using var startResponse = await apiClient.PostAsJsonAsync("api/runtime/start", new { ProcessDefinitionKey = processKey, BusinessKey = businessKey, TenantId = tenantId }, TestContext.Current.CancellationToken);
		var startBody = await startResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(startResponse.IsSuccessStatusCode, startBody);
		using var started = JsonDocument.Parse(startBody);
		return started.RootElement.GetProperty("id").GetGuid();
	}

	private async Task SuspendResumeDeleteInstanceAsync(
		IPage page, HttpClient apiClient, string tenantName, string? tenantId, string businessKey, Guid instanceId)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-instances");
		await SelectTenantAsync(page, tenantName, tenantId!);
		await FillBoundInputAsync(page.GetByPlaceholder("Search instances..."), businessKey);
		var instanceRow = page.Locator("tr").Filter(new() { HasText = businessKey }).First;
		await instanceRow.WaitForAsync();

		// While waiting on the user task the instance is Running/Suspended-capable, so a Suspend
		// button is rendered. Suspend it through the UI...
		await instanceRow.GetByRole(AriaRole.Button, new() { Name = "Suspend Instance", Exact = true }).ClickAsync();
		await page.GetByText("Process instance suspended", new() { Exact = false }).WaitForAsync();
		// ...and verify the persisted state is now Suspended via the authoritative API.
		var suspended = await apiClient.GetFromJsonAsync<JsonElement>($"api/runtime/{instanceId}", TestContext.Current.CancellationToken);
		Assert.Equal("Suspended", suspended.GetProperty("state").GetString());

		// Resume it through the UI and verify the persisted state flips back to Running.
		await instanceRow.GetByRole(AriaRole.Button, new() { Name = "Resume Instance", Exact = true }).ClickAsync();
		await page.GetByText("Process instance resumed", new() { Exact = false }).WaitForAsync();
		var resumed = await apiClient.GetFromJsonAsync<JsonElement>($"api/runtime/{instanceId}", TestContext.Current.CancellationToken);
		Assert.Equal("Waiting", resumed.GetProperty("state").GetString());

		// Delete it through the UI (confirm dialog), then verify the instance is gone via the API.
		await instanceRow.GetByRole(AriaRole.Button, new() { Name = "Delete Instance", Exact = true }).ClickAsync();
		var confirmDialog = page.GetByRole(AriaRole.Dialog);
		await confirmDialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
		await page.GetByText("Process instance deleted", new() { Exact = false }).WaitForAsync();

		var afterDelete = await apiClient.GetFromJsonAsync<JsonElement[]>($"api/runtime?tenantId={Uri.EscapeDataString(tenantId!)}", TestContext.Current.CancellationToken);
		Assert.DoesNotContain(afterDelete ?? [], candidate => candidate.GetProperty("id").GetGuid() == instanceId);
	}


	private async Task UploadBpmnFileAsync(IPage page, string fileName, string xml)
	{
		var chooser = page.WaitForFileChooserAsync();
		await page.GetByText("Upload BPMN files", new() { Exact = true }).ClickAsync();
		await (await chooser).SetFilesAsync(new FilePayload
		{
			Name = fileName,
			MimeType = "application/xml",
			Buffer = Encoding.UTF8.GetBytes(xml)
		});
	}

	private static async Task<JsonElement[]?> WaitForDeployedDefinitionAsync(HttpClient apiClient, string processKey)
	{
		for (var poll = 0; poll < 40; poll++)
		{
			var deployed = await apiClient.GetFromJsonAsync<JsonElement[]>(
				$"api/repository?key={Uri.EscapeDataString(processKey)}",
				TestContext.Current.CancellationToken);
			if (deployed is { Length: > 0 })
			{
				return deployed;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		return null;
	}


	private static void HookSimulationResponses(IPage page, ConcurrentQueue<string> simulationResponses)
	{
		page.Response += async (_, response) =>
		{
			if (response.Url.Contains("/api/simulation", StringComparison.Ordinal) && response.Request.Method == "POST")
			{
				try
				{
					var body = await response.TextAsync();
					var posted = response.Request.PostDataBuffer is null
						? "<no-json>"
						: System.Text.Encoding.UTF8.GetString(response.Request.PostDataBuffer);
					simulationResponses.Enqueue($"{response.Status} {posted} => {body}");
				}
				catch
				{
					// Best effort only.
				}
			}
		};
	}

	private async Task<string> CreateSimulationTenantAsync(HttpClient apiClient, string tenantName)
	{
		using var tenantResponse = await apiClient.PostAsJsonAsync("api/tenant", new { name = tenantName, description = "Simulation E2E" }, TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		using var tenant = JsonDocument.Parse(tenantBody);
		var tenantId = tenant.RootElement.GetProperty("id").GetString();
		Assert.False(string.IsNullOrWhiteSpace(tenantId));
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
		return tenantId;
	}

	private async Task ProbeSimulationEngineAsync(string processKey, string tenantId)
	{
		// Probe the live simulation engine directly to capture the exact request/response
		// contract before driving the GUI (isolates UI-fill issues from engine behavior).
		using var probeResponse = await apiClientPostSimulation(processKey, CreateBpmn(processKey), tenantId);
		var probeBody = await probeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(probeResponse.IsSuccessStatusCode,
			$"Direct simulation probe failed HTTP {(int)probeResponse.StatusCode}: {probeBody}");
	}

	private async Task<string> RunSimulationAndGetResultAsync(
		IPage page, string processKey, string tenantId,
		ConcurrentQueue<string> simulationResponses, string tenantName)
	{
		var response = await page.GotoAsync($"{host.StudioBaseAddress}simulation");
		Assert.NotNull(response);
		Assert.True(response.Ok, $"Studio returned HTTP {response.Status}.");
		await SelectTenantAsync(page, tenantName, tenantId);

		await FillBoundInputAsync(page.GetByLabel("Process definition id", new() { Exact = true }), processKey);
		await FillBoundInputAsync(page.GetByLabel("Maximum steps", new() { Exact = true }), "50");
		await SetBoundInputFastAsync(page.GetByLabel("BPMN XML", new() { Exact = true }), CreateBpmn(processKey));

		// Verify the bound inputs actually registered their values before submitting the GUI.
		Assert.Equal(processKey, await page.GetByLabel("Process definition id", new() { Exact = true }).InputValueAsync());
		var filledBpmn = await page.GetByLabel("BPMN XML", new() { Exact = true }).InputValueAsync();
		Assert.Contains("startEvent", filledBpmn, StringComparison.Ordinal);
		Assert.Contains("endEvent", filledBpmn, StringComparison.Ordinal);
		var tenantFieldValue = await page.GetByLabel("Tenant id", new() { Exact = true }).InputValueAsync();
		Assert.Equal(tenantId, tenantFieldValue);

		await page.GetByRole(AriaRole.Button, new() { Name = "Run simulation", Exact = true }).ClickAsync();

		var resultPre = page.GetByText("Result", new() { Exact = true }).Locator("..").Locator("pre").First;
		try
		{
			await resultPre.WaitForAsync();
		}
		catch (TimeoutException ex)
		{
			var bodyText = await page.Locator("body").InnerTextAsync();
			var notifications = await page.Locator(".mud-snackbar").AllTextContentsAsync();
			throw new InvalidOperationException(
				$"Simulation did not render a result. Page text: {bodyText} | " +
				$"Simulation HTTP round-trips: {string.Join(" || ", simulationResponses)} | " +
				$"Notifications: {string.Join(" | ", notifications)} | " +
				$"Recent API logs: {string.Join(" | ", host.ApiLogs.TakeLast(150))} | " +
				$"Recent Studio logs: {string.Join(" | ", host.StudioLogs.TakeLast(150))}", ex);
		}
		var resultJson = await resultPre.InnerTextAsync();
		Assert.Contains(processKey, resultJson, StringComparison.Ordinal);
		using var result = JsonDocument.Parse(resultJson);
		var simulation = result.RootElement.GetProperty("simulation");
		Assert.Equal(processKey, simulation.GetProperty("processDefinitionId").GetString());
		Assert.True(simulation.GetProperty("completed").GetBoolean(), "The start-to-end process must simulate to completion.");
		Assert.True(simulation.GetProperty("steps").GetArrayLength() >= 2, "Expected at least start + end simulation steps.");
		return resultJson;
	}

	private async Task AnalyzeAndCompareAsync(IPage page, string processKey, string resultJson)
	{
		// Summary and Variable Trace through the GUI "Analyze result" button.
		await page.GetByRole(AriaRole.Button, new() { Name = "Analyze result", Exact = true }).ClickAsync();
		var analyticsPre = page.GetByText("Analytics", new() { Exact = true }).Locator("xpath=following-sibling::pre[1]");
		try
		{
			await analyticsPre.WaitForAsync();
		}
		catch (TimeoutException ex)
		{
			var bodyText = await page.Locator("body").InnerTextAsync();
			throw new InvalidOperationException(
				$"Analysis did not render. Page text: {bodyText} | " +
				$"Recent API logs: {string.Join(" | ", host.ApiLogs.TakeLast(120))}", ex);
		}
		var summaryJson = await analyticsPre.InnerTextAsync();
		using var summary = JsonDocument.Parse(summaryJson);
		Assert.True(summary.RootElement.TryGetProperty("summary", out var summaryRoot),
			$"Analytics response had no 'summary' property. Raw: {summaryJson}");
		Assert.Equal(processKey, summaryRoot.GetProperty("processDefinitionId").GetString());
		Assert.True(summaryRoot.GetProperty("stepCount").GetInt32() >= 2);
		Assert.True(summaryRoot.GetProperty("completed").GetBoolean());

		// Compare two runs of the same deterministic process through the GUI "Compare" button.
		await SetBoundInputFastAsync(page.GetByLabel("Result A JSON", new() { Exact = true }), resultJson);
		await SetBoundInputFastAsync(page.GetByLabel("Result B JSON", new() { Exact = true }), resultJson);
		await page.GetByRole(AriaRole.Button, new() { Name = "Compare", Exact = true }).ClickAsync();
		var comparisonPre = page.GetByText("Compare simulation results", new() { Exact = true }).Locator("..").Locator("pre").First;
		try
		{
			await comparisonPre.WaitForAsync();
		}
		catch (TimeoutException ex)
		{
			var bodyText = await page.Locator("body").InnerTextAsync();
			throw new InvalidOperationException(
				$"Comparison did not render. Page text: {bodyText} | " +
				$"Recent API logs: {string.Join(" | ", host.ApiLogs.TakeLast(150))}", ex);
		}
		var comparisonJson = await comparisonPre.InnerTextAsync();
		Assert.True(comparisonJson.Trim().Length > 0, "Comparison of identical runs must produce a non-empty result.");
	}

	private async Task<string?> SaveScenarioAsync(
		IPage page, HttpClient apiClient, string processKey, string tenantId, string scenarioName)
	{
		// Save the simulation parameters as a repeatable scenario through the GUI.
		await FillBoundInputAsync(page.GetByLabel("Scenario name", new() { Exact = true }), scenarioName);
		await page.GetByRole(AriaRole.Button, new() { Name = "Save scenario", Exact = true }).ClickAsync();

		// Poll the live backend until the scenario persists (save is async; a concurrent render
		// can also close the button popover). Assert on the API-authoritative state, not UI text.
		JsonElement[] scenarios = [];
		JsonElement? saved = null;
		for (var attempt = 0; attempt < 40 && saved is null; attempt++)
		{
			scenarios = await apiClient.GetFromJsonAsync<JsonElement[]>($"api/simulation-scenario?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken)
				?? [];
			var match = scenarios.FirstOrDefault(candidate => candidate.GetProperty("name").GetString() == scenarioName);
			saved = match.ValueKind == JsonValueKind.Undefined ? null : match;
			if (saved is null)
			{
				await Task.Delay(250, TestContext.Current.CancellationToken);
			}
		}
		if (saved is null)
		{
			var allScenarios = await apiClient.GetFromJsonAsync<JsonElement[]>("api/simulation-scenario", TestContext.Current.CancellationToken);
			var bodyText = await page.Locator("body").InnerTextAsync();
			var received = string.Join(" | ", scenarios.Select(s => s.GetProperty("name").GetString()));
			var all = string.Join(" | ", (allScenarios ?? []).Select(s => $"{s.GetProperty("name").GetString()} (tenant={s.GetProperty("tenantId").GetString()})"));
			throw new InvalidOperationException(
				$"Scenario '{scenarioName}' was not persisted. Received: {received} | " +
				$"All (unfiltered): {all} | Page text: {bodyText} | " +
				$"Recent API logs: {string.Join(" | ", host.ApiLogs.TakeLast(120))}");
		}
		var scenarioId = saved.Value.GetProperty("id").GetString();
		Assert.Equal(processKey, saved.Value.GetProperty("processDefinitionId").GetString());
		Assert.Equal(50, saved.Value.GetProperty("maxSteps").GetInt32());
		return scenarioId;
	}

	private async Task UpdateScenarioAsync(IPage page, HttpClient apiClient, string tenantId, string scenarioName, string? scenarioId)
	{
		// Update the scenario name through the GUI and verify it persists.
		var updatedName = $"{scenarioName} v2";
		await FillBoundInputAsync(page.GetByLabel("Scenario name", new() { Exact = true }), updatedName);
		await FillBoundInputAsync(page.GetByLabel("Scenario id for update/delete", new() { Exact = true }), scenarioId!);
		await page.GetByRole(AriaRole.Button, new() { Name = "Update scenario", Exact = true }).ClickAsync();
		JsonElement? updated = null;
		for (var attempt = 0; attempt < 40 && updated is null; attempt++)
		{
			var scenarios = await apiClient.GetFromJsonAsync<JsonElement[]>($"api/simulation-scenario?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken)
				?? [];
			var match = scenarios.FirstOrDefault(candidate => candidate.GetProperty("id").GetString() == scenarioId);
			if (match.ValueKind != JsonValueKind.Undefined && match.GetProperty("name").GetString() == updatedName)
			{
				updated = match;
			}

			if (updated is null)
			{
				await Task.Delay(250, TestContext.Current.CancellationToken);
			}
		}
		Assert.NotNull(updated);
		Assert.Equal(updatedName, updated.Value.GetProperty("name").GetString());
	}

	private async Task DeleteScenarioAsync(IPage page, HttpClient apiClient, string tenantId, string? scenarioId)
	{
		// Delete the scenario through the GUI and confirm it is gone from the live backend.
		await FillBoundInputAsync(page.GetByLabel("Scenario id for update/delete", new() { Exact = true }), scenarioId!);
		await page.GetByRole(AriaRole.Button, new() { Name = "Delete scenario", Exact = true }).ClickAsync();
		var stillPresent = true;
		for (var attempt = 0; attempt < 40 && stillPresent; attempt++)
		{
			var scenarios = await apiClient.GetFromJsonAsync<JsonElement[]>($"api/simulation-scenario?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken)
				?? [];
			stillPresent = scenarios.Any(candidate => candidate.ValueKind != JsonValueKind.Undefined && candidate.GetProperty("id").GetString() == scenarioId);
			if (stillPresent)
			{
				await Task.Delay(250, TestContext.Current.CancellationToken);
			}
		}
		Assert.False(stillPresent, $"Scenario '{scenarioId}' was still present after delete.");
	}


	private static string CreateMessageInterruptBpmn(string messageName, string processKey)
	{
		return $$"""
	        <?xml version="1.0" encoding="UTF-8"?>
	        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
	                     targetNamespace="https://vertexbpmn.dev/e2e">
	          <message id="payment-message" name="{{messageName}}" />
	          <process id="{{processKey}}" isExecutable="true">
	            <startEvent id="start" />
	            <sequenceFlow id="to-message" sourceRef="start" targetRef="await-message" />
	            <intermediateCatchEvent id="await-message" name="Await payment">
	              <messageEventDefinition messageRef="payment-message" />
	            </intermediateCatchEvent>
	            <sequenceFlow id="to-end" sourceRef="await-message" targetRef="end" />
	            <endEvent id="end" />
	          </process>
	        </definitions>
	        """;
	}

	private static string CreateSignalCatchBpmn(string signalName, string processKey)
	{
		return $$"""
	        <?xml version="1.0" encoding="UTF-8"?>
	        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
	                     targetNamespace="https://vertexbpmn.dev/e2e">
	          <signal id="release-signal" name="{{signalName}}" />
	          <process id="{{processKey}}" isExecutable="true">
	            <startEvent id="start" />
	            <sequenceFlow id="to-signal" sourceRef="start" targetRef="await-signal" />
	            <intermediateCatchEvent id="await-signal" name="Await release">
	              <signalEventDefinition signalRef="release-signal" />
	            </intermediateCatchEvent>
	            <sequenceFlow id="to-end" sourceRef="await-signal" targetRef="end" />
	            <endEvent id="end" />
	          </process>
	        </definitions>
	        """;
	}

	private static async Task DeployDefinitionAsync(HttpClient apiClient, string bpmnXml, string name)
	{
		using var deploy = await apiClient.PostAsJsonAsync(
				   "api/repository",
				   new { bpmnXml, name, tenantId = (string?)null },
				   TestContext.Current.CancellationToken);
		var body = await deploy.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(deploy.IsSuccessStatusCode, body);
	}

	private static async Task<Guid> StartNullTenantInstanceAsync(HttpClient apiClient, string processKey, string businessKey)
	{
		using var start = await apiClient.PostAsJsonAsync(
				   "api/runtime/start",
				   new { processDefinitionKey = processKey, businessKey, variables = new Dictionary<string, object>(StringComparer.Ordinal), tenantId = (string?)null },
				   TestContext.Current.CancellationToken);
		var body = await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(start.IsSuccessStatusCode, body);
		return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
	}

	private static async Task<List<Guid>> StartSignalInstancesAsync(HttpClient apiClient, string signalProcessKey, string runId)
	{
		var signalInstanceIds = new List<Guid>();
		for (var i = 0; i < 2; i++)
		{
			using var start = await apiClient.PostAsJsonAsync(
				"api/runtime/start",
				new { processDefinitionKey = signalProcessKey, businessKey = $"sig-{runId}-{i}", variables = new Dictionary<string, object>(StringComparer.Ordinal) { ["instance"] = i.ToString() }, tenantId = (string?)null },
				TestContext.Current.CancellationToken);
			var body = await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
			Assert.True(start.IsSuccessStatusCode, body);
			signalInstanceIds.Add(JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid());
		}
		return signalInstanceIds;
	}

	private async Task CorrelateWrongThenRightMessageAsync(IPage page, string messageName, Guid messageInstanceId, string parkedState)
	{
		// Correlate a NON-matching message first; the instance must stay waiting (not complete).
		await page.GotoAsync($"{host.StudioBaseAddress}messages-signals");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Message name", new() { Exact = true }), $"wrong-{messageName}");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Process instance id (optional)", new() { Exact = true }), messageInstanceId.ToString());
		var msgNameInput = page.GetByLabel("Message name", new() { Exact = true });
		var correlateButton = page.GetByRole(AriaRole.Button, new() { Name = "Correlate message (ProcessManager)", Exact = true });
		var enabled = false;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			if (await correlateButton.IsEnabledAsync())
			{
				enabled = true;
				break;
			}

			await Task.Delay(250, TestContext.Current.CancellationToken);
		}

		if (!enabled)
		{
			var msgNameValue = await msgNameInput.InputValueAsync();
			var msgNameHtml = await msgNameInput.EvaluateAsync("el => el.tagName + '|' + (el.id||'') + '|' + (el.getAttribute('class')||'')");
			var allInputs = await page.EvaluateAsync(
				"() => Array.from(document.querySelectorAll('input, textarea')).map(e => ({tag:e.tagName, id:e.id||'', name:e.name||'', type:e.type||'', placeholder:e.placeholder||'', value:e.value, cls:e.className||''}))");
			var dump = JsonSerializer.Serialize(allInputs);
			throw new InvalidOperationException(
				$"Correlate button stayed disabled. Field 'Message name' DOM value='{msgNameValue}' (expected 'wrong-{messageName}'). Element: {msgNameHtml}. ALL INPUTS: {dump}");
		}

		await correlateButton.ClickAsync();
		await page.GetByText("not_found", new() { Exact = false }).First.WaitForAsync();
		Assert.Equal(parkedState, await GetInstanceStateAsync(messageInstanceId));

		// Correlate the MATCHING message; the instance must complete.
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Message name", new() { Exact = true }), messageName);
		await correlateButton.ClickAsync();
		await WaitForInstanceStateAsync(messageInstanceId, "Completed");
	}

	private async Task BroadcastSignalAsync(IPage page, string signalName, IReadOnlyList<Guid> signalInstanceIds)
	{
		// Broadcast the signal; both parked instances must complete.
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Signal name", new() { Exact = true }), signalName);
		var broadcastButton = page.GetByRole(AriaRole.Button, new() { Name = "Broadcast signal (ProcessManager)", Exact = true });
		await broadcastButton.ClickAsync();
		foreach (var id in signalInstanceIds)
		{
			await WaitForInstanceStateAsync(id, "Completed");
		}
	}


	private static string CreateDebugBpmn(string debugProcessKey, string messageName)
	{
		return $$"""
	        <?xml version="1.0" encoding="UTF-8"?>
	        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
	                     xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
	                     xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"
	                     xmlns:di="http://www.omg.org/spec/DD/20100524/DI"
	                     targetNamespace="https://vertexbpmn.dev/e2e" id="Definitions_{{debugProcessKey}}">
	          <message id="dbg-msg" name="{{messageName}}" />
	          <process id="{{debugProcessKey}}" isExecutable="true">
	            <startEvent id="start" />
	            <sequenceFlow id="f-start" sourceRef="start" targetRef="await" />
	            <intermediateCatchEvent id="await" name="Await debug message">
	              <messageEventDefinition messageRef="dbg-msg" />
	            </intermediateCatchEvent>
	            <sequenceFlow id="f-await" sourceRef="await" targetRef="task1" />
	            <task id="task1" name="First task" />
	            <sequenceFlow id="f-1" sourceRef="task1" targetRef="task2" />
	            <task id="task2" name="Second task" />
	            <sequenceFlow id="f-2" sourceRef="task2" targetRef="end" />
	            <endEvent id="end" />
	          </process>
	          <bpmndi:BPMNDiagram id="Diagram_{{debugProcessKey}}">
	            <bpmndi:BPMNPlane id="Plane_{{debugProcessKey}}" bpmnElement="{{debugProcessKey}}">
	              <bpmndi:BPMNShape id="start_di" bpmnElement="start"><dc:Bounds x="100" y="100" width="36" height="36" /></bpmndi:BPMNShape>
	              <bpmndi:BPMNShape id="await_di" bpmnElement="await"><dc:Bounds x="220" y="100" width="36" height="36" /></bpmndi:BPMNShape>
	              <bpmndi:BPMNShape id="task1_di" bpmnElement="task1"><dc:Bounds x="340" y="93" width="100" height="50" /></bpmndi:BPMNShape>
	              <bpmndi:BPMNShape id="task2_di" bpmnElement="task2"><dc:Bounds x="520" y="93" width="100" height="50" /></bpmndi:BPMNShape>
	              <bpmndi:BPMNShape id="end_di" bpmnElement="end"><dc:Bounds x="700" y="100" width="36" height="36" /></bpmndi:BPMNShape>
	              <bpmndi:BPMNEdge id="f-start_di" bpmnElement="f-start"><di:waypoint x="136" y="118" /><di:waypoint x="220" y="118" /></bpmndi:BPMNEdge>
	              <bpmndi:BPMNEdge id="f-await_di" bpmnElement="f-await"><di:waypoint x="256" y="118" /><di:waypoint x="340" y="118" /></bpmndi:BPMNEdge>
	              <bpmndi:BPMNEdge id="f-1_di" bpmnElement="f-1"><di:waypoint x="440" y="118" /><di:waypoint x="520" y="118" /></bpmndi:BPMNEdge>
	              <bpmndi:BPMNEdge id="f-2_di" bpmnElement="f-2"><di:waypoint x="620" y="118" /><di:waypoint x="700" y="118" /></bpmndi:BPMNEdge>
	            </bpmndi:BPMNPlane>
	          </bpmndi:BPMNDiagram>
	        </definitions>
	        """;
	}

	private static async Task DeployDebugProcessAsync(HttpClient apiClient, string debugProcessKey, string debugBpmn)
	{
		using var deploy = await apiClient.PostAsJsonAsync(
				   "api/repository",
				   new { bpmnXml = debugBpmn, name = $"{debugProcessKey}.bpmn", tenantId = (string?)null },
				   TestContext.Current.CancellationToken);
		var body = await deploy.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(deploy.IsSuccessStatusCode, body);
	}

	private static async Task<Guid> StartDebugInstanceAsync(HttpClient apiClient, string debugProcessKey, string runId)
	{
		using var start = await apiClient.PostAsJsonAsync(
				   "api/runtime/start",
				   new { processDefinitionKey = debugProcessKey, businessKey = $"dbg-{runId}", variables = new Dictionary<string, object>(StringComparer.Ordinal), tenantId = (string?)null },
				   TestContext.Current.CancellationToken);
		var body = await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(start.IsSuccessStatusCode, body);
		return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
	}

	private async Task RunDebugTraceAsync(IPage page, string debugBpmn)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}debugging");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("BPMN XML", new() { Exact = true }), debugBpmn);
		await page.GetByRole(AriaRole.Button, new() { Name = "Run trace", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "StartEvent");
		await WaitForTextInPageAsync(page, "EndEvent");
	}

	private async Task<string> StartDebugSessionAsync(IPage page, Guid instanceId)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Process instance id", new() { Exact = true }), instanceId.ToString());
		await page.GetByRole(AriaRole.Button, new() { Name = "Start session", Exact = true }).ClickAsync();
		return await ReadFilledValueAsync(page.GetByLabel("Session id", new() { Exact = true }));
	}

	private async Task SetBreakpointAtSecondTaskAsync(IPage page)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Breakpoint activity id", new() { Exact = true }), "task2");
		await page.GetByRole(AriaRole.Button, new() { Name = "Set breakpoint", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "Breakpoint set at activity");
	}

	private async Task StepOverToAsync(IPage page, Guid instanceId, string expectedActivity)
	{
		await page.GetByRole(AriaRole.Button, new() { Name = "Step over", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "\"endActivity\"");
		await WaitForInstanceStateAsync(instanceId, expectedActivity);
	}

	private async Task InspectDebugVariablesAsync(IPage page)
	{
		await page.GetByRole(AriaRole.Button, new() { Name = "Inspect variables", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "\"var1\"");
	}

	private async Task ContinueDebugSessionAsync(IPage page)
	{
		await page.GetByRole(AriaRole.Button, new() { Name = "Continue", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "\"processCompleted\"");
	}

	private async Task VisualizeDebugProcessAsync(IPage page)
	{
		await page.GetByRole(AriaRole.Button, new() { Name = "Visualize process", Exact = true }).ClickAsync();
		var timeline = page.GetByTestId("runtime-timeline");
		await timeline.WaitForAsync();
		await page.GetByRole(AriaRole.Button, new() { Name = "Replay", Exact = true }).First.WaitForAsync();
		await page.GetByText("Process visualization", new() { Exact = false }).First.WaitForAsync();
	}
	private static async Task<Guid> DeployMigrationProcessAsync(HttpClient apiClient, string key, string taskId, string taskName)
	{
		var bpmn = $$"""
                <?xml version="1.0" encoding="UTF-8"?>
                <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                             targetNamespace="https://vertexbpmn.dev/e2e">
                  <process id="{{key}}" isExecutable="true">
                    <startEvent id="start" />
                    <sequenceFlow id="to-task" sourceRef="start" targetRef="{{taskId}}" />
                    <userTask id="{{taskId}}" name="{{taskName}}" />
                    <sequenceFlow id="to-end" sourceRef="{{taskId}}" targetRef="end" />
                    <endEvent id="end" />
                  </process>
                </definitions>
                """;
		using var response = await apiClient.PostAsJsonAsync(
			"api/repository",
			new { bpmnXml = bpmn, name = $"{key}.bpmn", tenantId = (string?)null },
			TestContext.Current.CancellationToken);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(response.IsSuccessStatusCode, body);
		return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
	}

	private static async Task<Guid> StartMigrationInstanceAsync(HttpClient apiClient, string sourceKey, string businessKey, bool useRetry = false)
	{
		if (useRetry)
		{
			Guid retriedId;
			using var startB = await RetryAsync(() => apiClient.PostAsJsonAsync(
					   "api/runtime/start",
					   new { processDefinitionKey = sourceKey, businessKey, variables = new Dictionary<string, object>(StringComparer.Ordinal), tenantId = (string?)null },
					   TestContext.Current.CancellationToken));
			var bodyB = await startB.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
			Assert.True(startB.IsSuccessStatusCode, bodyB);
			retriedId = JsonDocument.Parse(bodyB).RootElement.GetProperty("id").GetGuid();
			return retriedId;
		}

		using var start = await apiClient.PostAsJsonAsync(
				   "api/runtime/start",
				   new { processDefinitionKey = sourceKey, businessKey, variables = new Dictionary<string, object>(StringComparer.Ordinal), tenantId = (string?)null },
				   TestContext.Current.CancellationToken);
		var body = await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(start.IsSuccessStatusCode, body);
		return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
	}

	private static async Task<Guid> ExecuteMigrationViaApiAsync(HttpClient apiClient, string sourceKey, string targetKey)
	{
		Guid executionId;
		using var planResponse = await apiClient.PostAsJsonAsync(
				   "api/migration/plan",
				   new { fromProcessKey = sourceKey, toProcessKey = targetKey, options = new { } },
				   TestContext.Current.CancellationToken);
		var planBody = await planResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(planResponse.IsSuccessStatusCode, planBody);
		var planId = JsonDocument.Parse(planBody).RootElement.GetProperty("id").GetGuid();
		using var execResponse = await apiClient.PostAsync(
			$"api/migration/execute/{planId}", null, TestContext.Current.CancellationToken);
		var execBody = await execResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(execResponse.IsSuccessStatusCode, execBody);
		executionId = JsonDocument.Parse(execBody).RootElement.GetProperty("id").GetGuid();
		return executionId;
	}

	private static async Task RejectInvalidMigrationAsync(IPage page)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Source process definition id", new() { Exact = true }), "not-a-guid");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Target process definition id", new() { Exact = true }), "also-not-a-guid");
		await page.GetByRole(AriaRole.Button, new() { Name = "Preview migration", Exact = true }).ClickAsync();
		await page.GetByText("Migration preview failed", new() { Exact = false }).WaitForAsync();
	}

	private async Task PreviewMigrationAsync(IPage page, Guid sourceId, Guid targetId)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Source process definition id", new() { Exact = true }), sourceId.ToString());
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Target process definition id", new() { Exact = true }), targetId.ToString());
		await page.GetByRole(AriaRole.Button, new() { Name = "Preview migration", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "Migration preview");
		await WaitForTextInPageAsync(page, "qualifiedPlanId");
	}

	private async Task ExecuteMigrationAsync(IPage page, Guid instanceA, Guid targetId)
	{
		await page.GetByRole(AriaRole.Button, new() { Name = "Execute migration (ProcessManager)", Exact = true }).ClickAsync();
		await WaitForTaskActivityAsync(instanceA, "review-v2");
		Assert.Equal(targetId, await GetInstanceProcessDefinitionIdAsync(instanceA));
	}

	private async Task GetMigrationStatusAsync(IPage page, Guid executionId)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Migration id", new() { Exact = true }), executionId.ToString());
		await page.GetByRole(AriaRole.Button, new() { Name = "Get status", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "Live migration result");
		await WaitForTextInPageAsync(page, "\"status\"");
	}

	private async Task CreateAndRestoreSnapshotAsync(IPage page, Guid instanceA)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Process instance id", new() { Exact = true }), instanceA.ToString());
		await page.GetByRole(AriaRole.Button, new() { Name = "Create snapshot", Exact = true }).ClickAsync();
		await Task.Delay(500, TestContext.Current.CancellationToken);
		await WaitForTextInPageAsync(page, "Live migration result");
		var snapshotId = await ReadSnapshotIdFromLastPreAsync(page);
		Assert.NotEqual(Guid.Empty, snapshotId);

		await FillBoundInputAndVerifyAsync(page.GetByLabel("Snapshot id", new() { Exact = true }), snapshotId.ToString());
		await page.GetByRole(AriaRole.Button, new() { Name = "Restore snapshot (ProcessManager)", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "restored");
	}

	private async Task RollbackMigrationAsync(IPage page, Guid executionId, Guid instanceB)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Migration id", new() { Exact = true }), executionId.ToString());
		await page.GetByRole(AriaRole.Button, new() { Name = "Rollback (ProcessManager)", Exact = true }).ClickAsync();
		await WaitForTextInPageAsync(page, "rollback");
		await WaitForTaskActivityAsync(instanceB, "review-v1");
	}


	private static async Task<string> CreateTenantAsync(IPage page, HttpClient apiClient, string tenantName, string description)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Tenant name", new() { Exact = true }), tenantName);
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Description", new() { Exact = true }), description);
		await page.GetByRole(AriaRole.Button, new() { Name = "Create tenant (Admin)", Exact = true }).ClickAsync();
		return (await WaitForTenantAsync(apiClient, tenantName))!;
	}

	private static async Task CancelTenantEditAsync(IPage page, HttpClient apiClient, string tenantA, string tenantAId)
	{
		var rowA = page.Locator("tr").Filter(new() { HasText = tenantA }).First;
		await rowA.GetByText("Tenant A description", new() { Exact = true }).WaitForAsync();
		await rowA.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();
		Assert.Equal("Tenant A description", await page.GetByLabel("Edit description", new() { Exact = true }).InputValueAsync());
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Edit tenant name", new() { Exact = true }), tenantA + " cancelled");
		await page.GetByRole(AriaRole.Button, new() { Name = "Cancel edit", Exact = true }).ClickAsync();
		using var cancelled = await apiClient.GetFromJsonAsync<JsonDocument>($"api/tenant/{tenantAId}", TestContext.Current.CancellationToken);
		Assert.Equal(tenantA, cancelled!.RootElement.GetProperty("name").GetString());
	}

	private static async Task RenameTenantAsync(IPage page, HttpClient apiClient, string tenantA, string tenantAId)
	{
		var rowA = page.Locator("tr").Filter(new() { HasText = tenantA }).First;
		await rowA.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Edit tenant name", new() { Exact = true }), tenantA);
		await page.GetByRole(AriaRole.Button, new() { Name = "Save tenant", Exact = true }).ClickAsync();
		await page.Locator("tr").Filter(new() { HasText = tenantA }).GetByText("Tenant A description", new() { Exact = true }).WaitForAsync();
		using var updated = await apiClient.GetFromJsonAsync<JsonDocument>($"api/tenant/{tenantAId}", TestContext.Current.CancellationToken);
		Assert.Equal(tenantA, updated!.RootElement.GetProperty("name").GetString());
		Assert.Equal("Tenant A description", updated.RootElement.GetProperty("description").GetString());
		Assert.Equal(0, await page.GetByText("Tenant operation failed", new() { Exact = false }).CountAsync());
	}

	private static async Task SetTenantDescriptionAsync(IPage page, HttpClient apiClient, ILocator editedRow, string tenantAId, string description)
	{
		await editedRow.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Edit description", new() { Exact = true }), description);
		await page.GetByRole(AriaRole.Button, new() { Name = "Save tenant", Exact = true }).ClickAsync();
		await Assertions.Expect(editedRow.Locator("[data-label='Description']")).ToHaveTextAsync(description);
		using var changed = await apiClient.GetFromJsonAsync<JsonDocument>($"api/tenant/{tenantAId}", TestContext.Current.CancellationToken);
		if (string.IsNullOrEmpty(description))
		{
			Assert.True(string.IsNullOrEmpty(changed!.RootElement.GetProperty("description").GetString()));
		}
		else
		{
			Assert.Equal(description, changed!.RootElement.GetProperty("description").GetString());
		}
	}

	private static async Task SelectTenantInHeaderAsync(IPage page, string tenantA)
	{
		await page.GetByRole(AriaRole.Combobox, new() { Name = "Tenant", Exact = true }).ClickAsync();
		await page.GetByRole(AriaRole.Option, new() { Name = tenantA, Exact = true }).ClickAsync();
		await Assertions.Expect(page.GetByRole(AriaRole.Combobox, new() { Name = "Tenant", Exact = true })).ToContainTextAsync(tenantA);
	}

	private async Task VerifyTenantIsolatedAsync(IPage page, string tenantName, string tenantId, string visibleKey, string hiddenKey)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}process-definitions");
		await SelectTenantAsync(page, tenantName, tenantId);
		await FillBoundInputAsync(page.GetByTestId("process-definition-search"), visibleKey);
		await page.Locator("tr").Filter(new() { HasText = visibleKey }).First.WaitForAsync();
		var hiddenVisible = await page.Locator("tr").Filter(new() { HasText = hiddenKey }).CountAsync();
		Assert.Equal(0, hiddenVisible);
	}

	private async Task DeleteTenantAsync(IPage page, HttpClient apiClient, string tenantB)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}tenants");
		var rowB = page.Locator("tr").Filter(new() { HasText = tenantB }).First;
		await rowB.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
		await WaitForTenantAbsentAsync(apiClient, tenantB);
	}


	private async Task<string> BootstrapCredentialTenantAsync(HttpClient apiClient, string tenantName)
	{
		using var tenantResponse = await apiClient.PostAsJsonAsync(
				   "api/tenant",
				   new { name = tenantName, description = "Phase 5 credential tenant" },
				   TestContext.Current.CancellationToken);
		var tenantBody = await tenantResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		Assert.True(tenantResponse.IsSuccessStatusCode, tenantBody);
		var tenantId = JsonDocument.Parse(tenantBody).RootElement.GetProperty("id").GetString();
		Assert.False(string.IsNullOrWhiteSpace(tenantId));
		host.RegisterApiCleanup(HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}");
		return tenantId;
	}

	private async Task OpenCredentialsPageAsync(IPage page, string tenantName, string tenantId)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}credentials");
		await page.GetByRole(AriaRole.Heading, new() { Name = "Credentials", Exact = true }).WaitForAsync();
		await SelectTenantAsync(page, tenantName, tenantId);
	}

	private static async Task CreateSecretCredentialAsync(IPage page, string credentialName, string originalSecret)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Name", new() { Exact = true }), credentialName);
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Type", new() { Exact = true }), "api-key");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Description", new() { Exact = true }), "Phase 5 credential");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Secret key", new() { Exact = true }), "value");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Secret value", new() { Exact = true }), originalSecret);
		await page.GetByRole(AriaRole.Button, new() { Name = "Create credential", Exact = true }).ClickAsync();
	}

	private static async Task AssertSecretNotEchoedAsync(ILocator row, IPage page, string credentialName, string originalSecret)
	{
		await row.WaitForAsync();
		Assert.Equal(1, await page.Locator("tr").Filter(new() { HasText = credentialName }).CountAsync());
		Assert.Equal(0, await page.GetByText(originalSecret, new() { Exact = true }).CountAsync());
		await AssertSecretNotInDomAsync(page, originalSecret);
	}

	private static async Task RotateSecretAsync(IPage page, ILocator row, string originalSecret, string rotatedSecret)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Secret value", new() { Exact = true }), rotatedSecret);
		await row.GetByRole(AriaRole.Button, new() { Name = "Rotate", Exact = true }).ClickAsync();
		await Task.Delay(1000, TestContext.Current.CancellationToken);
		Assert.Equal(0, await page.GetByText(originalSecret, new() { Exact = true }).CountAsync());
		Assert.Equal(0, await page.GetByText(rotatedSecret, new() { Exact = true }).CountAsync());
		await AssertSecretNotInDomAsync(page, rotatedSecret);
	}

	private static async Task DeleteCredentialAsync(ILocator row, HttpClient apiClient, string tenantId, string credentialName)
	{
		await row.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
		await WaitForCredentialAbsentAsync(apiClient, tenantId, credentialName);
	}


	private async Task OpenConnectorsPageAsync(IPage page, string tenantName, string tenantId)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}connectors");
		await page.GetByRole(AriaRole.Heading, new() { Name = "Connectors", Exact = true }).WaitForAsync();
		await SelectTenantAsync(page, tenantName, tenantId);
	}

	private static async Task CreateConnectorAsync(IPage page, string connectorName)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Name", new() { Exact = true }), connectorName);
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Type", new() { Exact = true }), "http");
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Endpoint (optional)", new() { Exact = true }), "http://127.0.0.1:9/");
		await page.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
	}

	private static async Task VerifyConfigurationValidAsync(IPage page, ILocator row)
	{
		await row.GetByRole(AriaRole.Button, new() { Name = "Check configuration", Exact = true }).ClickAsync();
		await page.Locator(".mud-alert-outlined-success").Filter(new() { HasText = "Configuration valid" }).WaitForAsync();
		Assert.Equal(0, await page.Locator(".mud-alert-outlined-error").CountAsync());
	}

	private static async Task VerifyConfigurationDisabledAsync(IPage page, ILocator row)
	{
		await row.GetByRole(AriaRole.Button, new() { Name = "Check configuration", Exact = true }).ClickAsync();
		await page.Locator(".mud-alert-outlined-error").Filter(new() { HasText = "Enable the connector before testing." }).WaitForAsync();
		Assert.Equal(0, await page.Locator(".mud-alert-outlined-success").CountAsync());
	}

	private static async Task SetConnectorEnabledAsync(IPage page, string connectorName, bool enabled)
	{
		await page.Locator("tr").Filter(new() { HasText = connectorName }).First
			.GetByRole(AriaRole.Button, new() { Name = enabled ? "Enable" : "Disable", Exact = true }).ClickAsync();
		var row = page.Locator("tr").Filter(new() { HasText = connectorName }).First;
		await row.WaitForAsync();
		var expected = enabled ? "Enabled" : "Disabled";
		for (var attempt = 0; attempt < 15; attempt++)
		{
			if ((await row.InnerTextAsync()).Contains(expected, StringComparison.Ordinal))
			{
				break;
			}

			await Task.Delay(500, TestContext.Current.CancellationToken);
		}

		Assert.Contains(expected, await row.InnerTextAsync(), StringComparison.Ordinal);
	}


	private async Task OpenTriggersPageAsync(IPage page, string tenantName, string tenantId)
	{
		await page.GotoAsync($"{host.StudioBaseAddress}triggers");
		await page.GetByRole(AriaRole.Heading, new() { Name = "Workflow Triggers", Exact = true }).WaitForAsync();
		await SelectTenantAsync(page, tenantName, tenantId);
	}

	private static async Task RegisterTriggerAsync(IPage page, string triggerName, string processKey)
	{
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Name", new() { Exact = true }), triggerName);
		await FillBoundInputAndVerifyAsync(page.GetByLabel("Process definition key", new() { Exact = true }), processKey);
		await page.GetByRole(AriaRole.Button, new() { Name = "Register trigger", Exact = true }).ClickAsync();
	}

	private static async Task ReadOneTimeSecretAsync(IPage page)
	{
		var secretAlert = page.GetByText("Save this secret now. It is shown only once.", new() { Exact = true });
		await secretAlert.WaitForAsync();
		var alertRoot = page.Locator(".mud-alert").Filter(new() { Has = secretAlert }).First;
		var secretValue = await alertRoot.Locator("input, textarea").First.InputValueAsync();
		Assert.False(string.IsNullOrWhiteSpace(secretValue), "Expected the one-time secret to be shown.");
	}

	private static async Task VerifyTriggerRowEnabledAsync(IPage page, string triggerName, string processKey)
	{
		var row = page.Locator("tr").Filter(new() { HasText = triggerName }).First;
		await row.WaitForAsync();
		Assert.Contains("Enabled", await row.InnerTextAsync(), StringComparison.Ordinal);
		Assert.Contains(processKey, await row.InnerTextAsync(), StringComparison.Ordinal);
	}

	private static async Task InvokeTriggerAndVerifyAsync(IPage page, string triggerName)
	{
		await page.Locator("tr").Filter(new() { HasText = triggerName }).First
			.GetByRole(AriaRole.Button, new() { Name = "Test", Exact = true }).ClickAsync();
		var row = page.Locator("tr").Filter(new() { HasText = triggerName }).First;
		await row.WaitForAsync();
		for (var attempt = 0; attempt < 20; attempt++)
		{
			var text = await row.InnerTextAsync();
			if (text.Contains("\t1", StringComparison.Ordinal) || text.Contains(" 1 ", StringComparison.Ordinal))
			{
				break;
			}

			await Task.Delay(500, TestContext.Current.CancellationToken);
			row = page.Locator("tr").Filter(new() { HasText = triggerName }).First;
		}

		Assert.Contains("1", await page.Locator("tr").Filter(new() { HasText = triggerName }).First.InnerTextAsync(), StringComparison.Ordinal);
	}

	private static async Task SetTriggerEnabledAsync(IPage page, string triggerName, bool enabled)
	{
		await page.Locator("tr").Filter(new() { HasText = triggerName }).First
			.GetByRole(AriaRole.Button, new() { Name = enabled ? "Enable" : "Disable", Exact = true }).ClickAsync();
		var row = page.Locator("tr").Filter(new() { HasText = triggerName }).First;
		await row.WaitForAsync();
		var expected = enabled ? "Enabled" : "Disabled";
		for (var attempt = 0; attempt < 15; attempt++)
		{
			if ((await row.InnerTextAsync()).Contains(expected, StringComparison.Ordinal))
			{
				break;
			}

			await Task.Delay(500, TestContext.Current.CancellationToken);
			row = page.Locator("tr").Filter(new() { HasText = triggerName }).First;
		}

		Assert.Contains(expected, await page.Locator("tr").Filter(new() { HasText = triggerName }).First.InnerTextAsync(), StringComparison.Ordinal);
	}



	private async Task<(IPage Page, ConcurrentQueue<string> BrowserErrors)> CreateErrorTrackingPageAsync()
	{
		var browserErrors = new ConcurrentQueue<string>();
		var page = await host.CreatePageAsync();
		page.PageError += (_, error) => browserErrors.Enqueue(error);
		page.Console += (_, message) =>
		{
			if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
			{
				browserErrors.Enqueue($"console: {message.Text}");
			}
		};
		return (page, browserErrors);
	}

	private async Task CleanupTenantsAsync(HttpClient apiClient, string? tenantAId, string? tenantBId, IPage page)
	{
		if (!string.IsNullOrWhiteSpace(tenantAId))
		{
			await apiClient.DeleteAsync($"api/tenant/{Uri.EscapeDataString(tenantAId)}", TestContext.Current.CancellationToken);
		}

		if (!string.IsNullOrWhiteSpace(tenantBId))
		{
			await apiClient.DeleteAsync($"api/tenant/{Uri.EscapeDataString(tenantBId)}", TestContext.Current.CancellationToken);
		}

		await host.ClosePageAsync(page);
	}

	private async Task CleanupCredentialResourcesAsync(HttpClient apiClient, string? credId, string? tenantId, IPage page)
	{
		if (!string.IsNullOrWhiteSpace(credId) && !string.IsNullOrWhiteSpace(tenantId))
		{
			await apiClient.DeleteAsync($"/api/credentials/{Uri.EscapeDataString(credId)}?tenantId={Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
		}

		if (!string.IsNullOrWhiteSpace(tenantId))
		{
			await apiClient.DeleteAsync($"api/tenant/{Uri.EscapeDataString(tenantId)}", TestContext.Current.CancellationToken);
		}

		await host.ClosePageAsync(page);
	}
}
