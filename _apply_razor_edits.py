import sys

p = "src/VertexBPMN.Studio/Components/Pages/BpmnModelerPage.razor"
data = open(p, "rb").read().decode("utf-8")
assert "\r\n" in data, "expected CRLF file"
original = data

def once(where, old, new, label):
    global data
    assert old in data, f"NOT FOUND: {label}"
    assert data.count(old) == 1, f"NOT UNIQUE ({data.count(old)}): {label}"
    data = data.replace(old, new, 1)

# 1) inject NavigationManager after Snackbar inject
once(data,
     "@inject MudBlazor.ISnackbar Snackbar\r\n@inject VertexBPMN.Studio.Services.ActiveEngineService ActiveEngineService",
     "@inject MudBlazor.ISnackbar Snackbar\r\n@inject NavigationManager Navigation\r\n@inject VertexBPMN.Studio.Services.ActiveEngineService ActiveEngineService",
     "inject NavigationManager")

# 2) field declaration
once(data,
     "    private Guid? _testRunInstanceId;\r\n",
     "    private Guid? _testRunInstanceId;\r\n    private IReadOnlyList<StudioCreatedWebhook> _createdWebhooks = [];\r\n",
     "field _createdWebhooks")

# 3) RunEngineTestAsync: deployed.Key -> deployed.Definition.Key
once(data,
     "await WorkflowService.StartProcessAsync(deployed.Key, variables,",
     "await WorkflowService.StartProcessAsync(deployed.Definition.Key, variables,",
     "RunEngineTestAsync .Key")

# 4) DeployBpmnXml body
once(data,
     "            await RepositoryService.DeployXmlAsync(xml, \"studio-model.bpmn\", TenantContext.CurrentTenantId);\r\n            await LoadDeployedDefinitionsAsync();\r\n            Snackbar.Add(\"BPMN deployed successfully.\", Severity.Success);",
     "            var deployed = await RepositoryService.DeployXmlAsync(xml, \"studio-model.bpmn\", TenantContext.CurrentTenantId);\r\n            _createdWebhooks = deployed.CreatedWebhooks;\r\n            await LoadDeployedDefinitionsAsync();\r\n            if (_createdWebhooks.Count > 0)\r\n                Snackbar.Add($\"{_createdWebhooks.Count} webhook trigger(s) created. Save the shown secrets now.\", Severity.Warning);\r\n            else\r\n                Snackbar.Add(\"BPMN deployed successfully.\", Severity.Success);",
     "DeployBpmnXml body")

# 5) markup block before n8n report
block = (
    "    @if (_createdWebhooks.Count > 0)\r\n"
    "    {\r\n"
    "        <MudPaper Class=\"pa-4 mb-4\" data-testid=\"bpmn-created-webhooks\">\r\n"
    "            <MudText Typo=\"Typo.h6\" GutterBottom=\"true\">Webhook trigger(s) created</MudText>\r\n"
    "            <MudText Typo=\"Typo.body2\" Color=\"Color.Secondary\" GutterBottom=\"true\">The secret is shown only once. Store it now; it is required to call the endpoint.</MudText>\r\n"
    "            @foreach (var hook in _createdWebhooks)\r\n"
    "            {\r\n"
    "                <MudAlert Severity=\"Severity.Warning\" Class=\"mb-3\">\r\n"
    "                    <MudText Typo=\"Typo.subtitle2\">@(hook.Method ?? \"POST\") @(hook.Path)</MudText>\r\n"
    "                    <MudText Typo=\"Typo.caption\">Invoke: @(Navigation.BaseUri.TrimEnd('/'))@hook.InvokePath</MudText>\r\n"
    "                    <MudTextField Value=\"@hook.Secret\" ReadOnly=\"true\" Variant=\"Variant.Outlined\" Label=\"Secret\" Class=\"mt-2\" />\r\n"
    "                    <MudText Typo=\"Typo.caption\">curl -X @(hook.Method ?? \"POST\") '@(Navigation.BaseUri.TrimEnd('/'))@hook.InvokePath' -H 'X-VertexBPMN-Trigger-Secret: @hook.Secret' -H 'Content-Type: application/json' -d '{}'</MudText>\r\n"
    "                </MudAlert>\r\n"
    "            }\r\n"
    "            <MudButton Variant=\"Variant.Text\" OnClick=\"@(() => _createdWebhooks = [])\">Dismiss</MudButton>\r\n"
    "        </MudPaper>\r\n"
    "    }\r\n"
)
once(data,
     "    </MudPaper>\r\n\r\n    @if (_n8nImportReport.Count > 0)",
     "    </MudPaper>\r\n\r\n" + block + "\r\n    @if (_n8nImportReport.Count > 0)",
     "markup block")

assert data != original
open(p, "w", encoding="utf-8", newline="").write(data)
print("OK: applied all 5 changes, CRLF preserved")
