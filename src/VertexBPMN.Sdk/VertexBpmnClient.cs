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

    private async Task<T?> SendForNullableAsync<T>(HttpMethod method, string uri, CancellationToken cancellationToken)
        => await SendForNullableAsync<T>(method, uri, null, cancellationToken);

    private async Task<T?> SendForNullableAsync<T>(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;

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

    private async Task SendWithoutContentAsync(HttpMethod method, string uri, object body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(options.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            request.Headers.Add("X-API-Key", options.ApiKey);
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

    private sealed record StartProcessRequest(string ProcessDefinitionKey, IDictionary<string, object?>? Variables, string? BusinessKey, string? TenantId);
    private sealed record UserRequest(string UserId);
    private sealed record VariablesRequest(IDictionary<string, object?>? Variables);
}