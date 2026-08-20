using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Sdk;

public sealed class VertexBpmnClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly VertexBpmnClientOptions options;

    public VertexBpmnClient(HttpClient httpClient, VertexBpmnClientOptions? options = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? new VertexBpmnClientOptions();
    }

    public async Task<EngineCapabilities> GetEngineCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var capabilities = await SendAsync<EngineCapabilities>(
            HttpMethod.Get, "api/engine/capabilities", cancellationToken)
            ?? throw new InvalidOperationException("The API returned no engine capabilities.");

        if (options.ExpectedEngineType is { } expected && capabilities.EngineType != expected)
        {
            throw new InvalidOperationException(
                $"Expected {expected} engine, but the API uses {capabilities.EngineType}.");
        }

        return capabilities;
    }

    public async Task<IReadOnlyList<ProcessDefinition>> ListProcessDefinitionsAsync(
        string? key = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri("api/vertex/process-definition", ("key", key), ("tenantId", tenantId ?? options.TenantId));
        return await SendAsync<List<ProcessDefinition>>(HttpMethod.Get, uri, cancellationToken) ?? [];
    }

    public Task<ProcessDefinition?> DeployProcessAsync(
        string bpmnXml,
        string name,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bpmnXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var request = new DeployProcessRequest(bpmnXml, name, tenantId ?? options.TenantId);
        return SendForNullableAsync<ProcessDefinition>(HttpMethod.Post, "api/repository", request, cancellationToken);
    }

    public Task<ProcessDefinition?> GetProcessDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
        => SendForNullableAsync<ProcessDefinition>(HttpMethod.Get, $"api/vertex/process-definition/{id}", cancellationToken);

    public async Task<IReadOnlyList<ProcessInstance>> ListProcessInstancesAsync(
        Guid? processDefinitionId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri("api/vertex/process-instance", ("processDefinitionId", processDefinitionId?.ToString()), ("tenantId", tenantId ?? options.TenantId));
        return await SendAsync<List<ProcessInstance>>(HttpMethod.Get, uri, cancellationToken) ?? [];
    }

    public Task<ProcessInstance?> GetProcessInstanceAsync(Guid id, CancellationToken cancellationToken = default)
        => SendForNullableAsync<ProcessInstance>(HttpMethod.Get, $"api/vertex/process-instance/{id}", cancellationToken);

    public Task<ProcessInstance?> StartProcessAsync(
        string processDefinitionKey,
        IDictionary<string, object?>? variables = null,
        string? businessKey = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processDefinitionKey);
        var request = new StartProcessRequest(processDefinitionKey, variables, businessKey, tenantId ?? options.TenantId);
        return SendForNullableAsync<ProcessInstance>(HttpMethod.Post, "api/runtime/start", request, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowTrigger>> ListWorkflowTriggersAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri("api/triggers", ("tenantId", tenantId ?? options.TenantId));
        return await SendAsync<List<WorkflowTrigger>>(HttpMethod.Get, uri, cancellationToken) ?? [];
    }

    public Task<WorkflowTriggerCreated?> CreateWorkflowTriggerAsync(
        string name,
        string processDefinitionKey,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(processDefinitionKey);
        var request = new CreateWorkflowTriggerRequest(name, processDefinitionKey, tenantId ?? options.TenantId);
        return SendForNullableAsync<WorkflowTriggerCreated>(HttpMethod.Post, "api/triggers", request, cancellationToken);
    }

    public Task UpdateWorkflowTriggerAsync(
        Guid triggerId,
        string? name = null,
        bool? enabled = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri($"api/triggers/{triggerId}", ("tenantId", tenantId ?? options.TenantId));
        return SendWithoutContentAsync(HttpMethod.Put, uri, new UpdateWorkflowTriggerRequest(name, enabled), cancellationToken);
    }

    public Task DeleteWorkflowTriggerAsync(
        Guid triggerId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri($"api/triggers/{triggerId}", ("tenantId", tenantId ?? options.TenantId));
        return SendWithoutContentAsync(HttpMethod.Delete, uri, null, cancellationToken);
    }

    public Task<ProcessInstance?> InvokeWorkflowTriggerAsync(
        Guid triggerId,
        string secret,
        IDictionary<string, object?>? variables = null,
        string? businessKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return SendForNullableAsync<ProcessInstance>(
            HttpMethod.Post,
            $"api/triggers/{triggerId}/invoke",
            new InvokeWorkflowTriggerRequest(variables, businessKey),
            cancellationToken,
            secret);
    }

    public async Task<IReadOnlyList<UserTask>> ListTasksAsync(
        Guid? processInstanceId = null,
        string? assignee = null,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri("api/vertex/task", ("processInstanceId", processInstanceId?.ToString()), ("assignee", assignee));
        return await SendAsync<List<UserTask>>(HttpMethod.Get, uri, cancellationToken) ?? [];
    }

    public Task ClaimTaskAsync(Guid taskId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return SendWithoutContentAsync(HttpMethod.Post, $"api/task/{taskId}/claim", new UserRequest(userId), cancellationToken);
    }

    public Task CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
        => SendWithoutContentAsync(HttpMethod.Post, $"api/task/{taskId}/complete", new VariablesRequest(variables), cancellationToken);

    public Task<FormSchema?> GetTaskFormSchemaAsync(Guid taskId, CancellationToken cancellationToken = default)
        => SendForNullableAsync<FormSchema>(HttpMethod.Get, $"api/vertex/task/{taskId}/form-schema", cancellationToken);

    public async Task<IReadOnlyList<CredentialMetadata>> ListCredentialsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
        => await SendAsync<List<CredentialMetadata>>(HttpMethod.Get, BuildUri("api/credentials", ("tenantId", tenantId ?? options.TenantId)), cancellationToken) ?? [];

    public Task<CredentialMetadata?> CreateCredentialAsync(string name, string type, IReadOnlyDictionary<string, string> secrets, string? description = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendForNullableAsync<CredentialMetadata>(HttpMethod.Post, "api/credentials", new CredentialWriteRequest(tenantId ?? options.TenantId, name, type, description, secrets), cancellationToken);

    public Task RotateCredentialSecretAsync(string credentialId, string key, string value, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendWithoutContentAsync(HttpMethod.Post, $"api/credentials/{Uri.EscapeDataString(credentialId)}/rotate", new CredentialSecretRotationRequest(tenantId ?? options.TenantId, key, value), cancellationToken);

    public async Task<IReadOnlyList<ConnectorMetadata>> ListConnectorsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
        => await SendAsync<List<ConnectorMetadata>>(HttpMethod.Get, BuildUri("api/connectors", ("tenantId", tenantId ?? options.TenantId)), cancellationToken) ?? [];

    public Task<ConnectorMetadata?> CreateConnectorAsync(ConnectorWriteRequest request, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendForNullableAsync<ConnectorMetadata>(HttpMethod.Post, "api/connectors", new ConnectorRequest(tenantId ?? options.TenantId, request.Name, request.Type, request.Description, request.Endpoint, request.CredentialId, request.TemplateId, request.Enabled), cancellationToken);

    public Task<ConnectorTestResult?> TestConnectorAsync(string connectorId, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendForNullableAsync<ConnectorTestResult>(HttpMethod.Post, $"api/connectors/{Uri.EscapeDataString(connectorId)}/test", new TenantRequest(tenantId ?? options.TenantId), cancellationToken);

    public async Task<IReadOnlyList<ConnectorTemplateMetadata>> ListConnectorTemplatesAsync(string? tenantId = null, CancellationToken cancellationToken = default)
        => await SendAsync<List<ConnectorTemplateMetadata>>(HttpMethod.Get, BuildUri("api/connector-templates", ("tenantId", tenantId ?? options.TenantId)), cancellationToken) ?? [];

    public Task<SemanticValidationResult?> ValidateBpmnAsync(string bpmnXml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bpmnXml);
        return SendForNullableAsync<SemanticValidationResult>(HttpMethod.Post, "api/diagnostics/bpmn", bpmnXml, cancellationToken);
    }

    public Task DeployDmnAsync(string decisionKey, string name, string dmnXml, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(dmnXml);
        return SendWithoutContentAsync(HttpMethod.Post, "api/decision/deploy", new DeployDecisionRequest(decisionKey, name, dmnXml, tenantId ?? options.TenantId), cancellationToken);
    }

    public Task<DecisionResult?> EvaluateDecisionAsync(string decisionKey, IDictionary<string, object?> inputs, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendForNullableAsync<DecisionResult>(HttpMethod.Post, "api/decision/evaluate", new EvaluateDecisionRequest(decisionKey, inputs, tenantId ?? options.TenantId), cancellationToken);

    public Task<FormDefinitionMetadata?> CreateFormAsync(string key, string name, string schema, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendForNullableAsync<FormDefinitionMetadata>(HttpMethod.Post, "api/forms", new FormWriteRequest(tenantId ?? options.TenantId, key, name, schema), cancellationToken);

    public async Task<TestRunResult> StartTestRunAsync(string bpmnXml, string name, IDictionary<string, object?>? variables = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bpmnXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return await SendForNullableAsync<TestRunResult>(HttpMethod.Post, "api/test-runs", new StartTestRunRequest(bpmnXml, name, variables, $"sdk-test-{Guid.NewGuid():N}", tenantId ?? options.TenantId), cancellationToken)
            ?? throw new InvalidOperationException("The API returned no test-run result.");
    }

    public Task<RuntimeExecutionTrace?> GetRuntimeTraceAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
        => SendForNullableAsync<RuntimeExecutionTrace>(HttpMethod.Get, $"api/visual-debug/trace/{processInstanceId}", cancellationToken);

    public Task<CaseDefinition?> DeployCmmnAsync(string key, string name, string cmmnXml, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendForNullableAsync<CaseDefinition>(HttpMethod.Post, "api/case-definitions/deploy", new DeployCaseRequest(key, name, cmmnXml, tenantId ?? options.TenantId), cancellationToken);

    public Task<CaseRunResult?> StartCaseAsync(string key, string? tenantId = null, CancellationToken cancellationToken = default)
        => SendForNullableAsync<CaseRunResult>(HttpMethod.Post, $"api/case-definitions/{Uri.EscapeDataString(key)}/start", new TenantRequest(tenantId ?? options.TenantId), cancellationToken);

    public Task<N8nImportResult?> ImportN8nWorkflowAsync(string workflowJson, CancellationToken cancellationToken = default)
        => ImportN8nWorkflowAsync(workflowJson, options.TenantId, cancellationToken);

    public Task<N8nImportResult?> ImportN8nWorkflowAsync(string workflowJson, string? tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowJson);
        return SendForNullableAsync<N8nImportResult>(HttpMethod.Post, "api/import/n8n", new N8nImportRequest(workflowJson, tenantId), cancellationToken);
    }

    private async Task<T?> SendForNullableAsync<T>(HttpMethod method, string uri, CancellationToken cancellationToken)
        => await SendForNullableAsync<T>(method, uri, null, cancellationToken);

    private async Task<T?> SendForNullableAsync<T>(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
        => await SendForNullableAsync<T>(method, uri, body, cancellationToken, null);

    private async Task<T?> SendForNullableAsync<T>(HttpMethod method, string uri, object? body, CancellationToken cancellationToken, string? triggerSecret)
    {
        using var request = CreateRequest(method, uri, body, triggerSecret);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string uri, object body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string uri, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task SendWithoutContentAsync(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? body = null, string? triggerSecret = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(options.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            request.Headers.Add("X-API-Key", options.ApiKey);
        if (!string.IsNullOrWhiteSpace(triggerSecret))
            request.Headers.Add("X-VertexBPMN-Trigger-Secret", triggerSecret);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private static string BuildUri(string path, params (string Name, string? Value)[] query)
    {
        var values = query
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Name)}={Uri.EscapeDataString(item.Value!)}")
            .ToArray();
        return values.Length == 0 ? path : $"{path}?{string.Join("&", values)}";
    }

    private sealed record DeployProcessRequest(string BpmnXml, string Name, string? TenantId);
    private sealed record StartProcessRequest(string ProcessDefinitionKey, IDictionary<string, object?>? Variables, string? BusinessKey, string? TenantId);
    private sealed record CreateWorkflowTriggerRequest(string Name, string ProcessDefinitionKey, string? TenantId);
    private sealed record UpdateWorkflowTriggerRequest(string? Name, bool? Enabled);
    private sealed record InvokeWorkflowTriggerRequest(IDictionary<string, object?>? Variables, string? BusinessKey);
    private sealed record UserRequest(string UserId);
    private sealed record VariablesRequest(IDictionary<string, object?>? Variables);
    private sealed record CredentialWriteRequest(string? TenantId, string Name, string Type, string? Description, IReadOnlyDictionary<string, string> Secrets);
    private sealed record CredentialSecretRotationRequest(string? TenantId, string Key, string Value);
    private sealed record ConnectorRequest(string? TenantId, string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId, bool Enabled);
    private sealed record TenantRequest(string? TenantId);
    private sealed record DeployDecisionRequest(string DecisionKey, string Name, string DmnXml, string? TenantId);
    private sealed record EvaluateDecisionRequest(string DecisionKey, IDictionary<string, object?> Inputs, string? TenantId);
    private sealed record FormWriteRequest(string? TenantId, string Key, string Name, string Schema);
    private sealed record StartTestRunRequest(string BpmnXml, string Name, IDictionary<string, object?>? Variables, string BusinessKey, string? TenantId);
    private sealed record DeployCaseRequest(string Key, string Name, string CmmnXml, string? TenantId);
    private sealed record N8nImportRequest(string WorkflowJson, string? TenantId);
}
