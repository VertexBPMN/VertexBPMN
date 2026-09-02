using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public class HttpBpmnEngineService : IBpmnEngineService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpBpmnEngineService> _logger;

    public HttpBpmnEngineService(IHttpClientFactory httpClientFactory, ILogger<HttpBpmnEngineService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("VertexBPMN.Api");
        _logger = logger;
    }

    public event Action<string>? OnEventEmitted;

    public async Task DeployAsync(IBrowserFile file, string? tenantId = null)
    {
        try
        {
            await using var stream = file.OpenReadStream(10 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            var bpmnXml = await reader.ReadToEndAsync();
            var request = new RepositoryDeployRequest(bpmnXml, file.Name, tenantId);

            var response = await _httpClient.PostAsJsonAsync("api/repository", request);
            response.EnsureSuccessStatusCode();
            
            OnEventEmitted?.Invoke($"Deployment created: {file.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying file {FileName}", file.Name);
            throw;
        }
    }

    public async Task<IEnumerable<Deployment>> GetDeploymentsAsync(string? tenantId = null)
    {
        try
        {
            var definitions = await _httpClient.GetFromJsonAsync<IEnumerable<ProcessDefinition>>(BuildRepositoryUri(tenantId));
            return definitions?.Select(definition => new Deployment
            {
                Id = definition.DeploymentId == Guid.Empty ? definition.Id.ToString() : definition.DeploymentId.ToString(),
                Name = definition.Name,
                DeploymentTime = definition.CreatedAt,
                TenantId = definition.TenantId
            }) ?? Enumerable.Empty<Deployment>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching deployments");
            return Enumerable.Empty<Deployment>();
        }
    }

    public async Task<ProcessDefinition> DeployXmlAsync(string xml, string name, string? tenantId = null)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/repository",
            new RepositoryDeployRequest(xml, name, tenantId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProcessDefinition>()
            ?? throw new InvalidOperationException("The API returned no deployed process definition.");
    }

    public async Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync(string? tenantId = null)
    {
        try
        {
            var definitions = await _httpClient.GetFromJsonAsync<IEnumerable<ProcessDefinition>>(BuildRepositoryUri(tenantId));
            return definitions ?? Enumerable.Empty<ProcessDefinition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process definitions");
            return Enumerable.Empty<ProcessDefinition>();
        }
    }

    private static string BuildRepositoryUri(string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? "api/repository"
            : $"api/repository?tenantId={Uri.EscapeDataString(tenantId)}";

    public async Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync(string? tenantId = null)
    {
        try
        {
            var uri = string.IsNullOrWhiteSpace(tenantId)
                ? "api/runtime"
                : $"api/runtime?tenantId={Uri.EscapeDataString(tenantId)}";
            var instances = await _httpClient.GetFromJsonAsync<IEnumerable<ProcessInstance>>(uri);
            return instances ?? Enumerable.Empty<ProcessInstance>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process instances");
            return Enumerable.Empty<ProcessInstance>();
        }
    }

    public async Task<IEnumerable<UserTask>> GetTasksAsync(string? tenantId = null)
    {
        try
        {
            var uri = string.IsNullOrWhiteSpace(tenantId)
                ? "api/task"
                : $"api/task?tenantId={Uri.EscapeDataString(tenantId)}";
            var tasks = await _httpClient.GetFromJsonAsync<IEnumerable<UserTask>>(uri);
            return tasks ?? Enumerable.Empty<UserTask>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tasks");
            return Enumerable.Empty<UserTask>();
        }
    }

    public async Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId, string? tenantId = null)
    {
        try
        {
            var response = await _httpClient.GetAsync(AddTenantQuery($"api/repository/{processDefinitionId}", tenantId));
            response.EnsureSuccessStatusCode();
            var definition = await response.Content.ReadFromJsonAsync<ProcessDefinition>();
            return definition?.BpmnXml ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process definition XML for {ProcessDefinitionId}", processDefinitionId);
            return string.Empty;
        }
    }

    public async Task<ProcessInstance> StartProcessAsync(
        string processDefinitionKey,
        IDictionary<string, object?>? variables = null,
        string? businessKey = null,
        string? tenantId = null)
    {
        var request = new StartProcessRequest(processDefinitionKey, variables, businessKey, tenantId);
        var response = await _httpClient.PostAsJsonAsync("api/runtime/start", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProcessInstance>()
            ?? throw new InvalidOperationException("The API returned no process instance.");
    }

    public async Task ClaimTaskAsync(Guid taskId, string userId, string? tenantId = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/task/{taskId}/claim", new UserRequest(userId, tenantId));
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, string? tenantId = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/task/{taskId}/complete", new VariablesRequest(variables, tenantId));
        response.EnsureSuccessStatusCode();
    }

    public async Task DelegateTaskAsync(Guid taskId, string userId, string? tenantId = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/task/{taskId}/delegate", new UserRequest(userId, tenantId));
        response.EnsureSuccessStatusCode();
    }

    public async Task SuspendProcessInstanceAsync(Guid instanceId, string? tenantId = null)
    {
        var response = await _httpClient.PostAsync(AddTenantQuery($"api/management/suspend-process-instance/{instanceId}", tenantId), null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResumeProcessInstanceAsync(Guid instanceId, string? tenantId = null)
    {
        var response = await _httpClient.PostAsync(AddTenantQuery($"api/management/resume-process-instance/{instanceId}", tenantId), null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProcessInstanceAsync(Guid instanceId, string? tenantId = null)
    {
        var response = await _httpClient.PostAsync(AddTenantQuery($"api/management/delete-process-instance/{instanceId}", tenantId), null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key, string? tenantId = null)
    {
        try
        {
            var versions = await _httpClient.GetFromJsonAsync<IEnumerable<ProcessDefinition>>(AddTenantQuery($"api/repository?key={Uri.EscapeDataString(key)}", tenantId));
            return versions ?? Enumerable.Empty<ProcessDefinition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process definition versions for {Key}", key);
            return Enumerable.Empty<ProcessDefinition>();
        }
    }

    public async Task DeleteProcessDefinitionAsync(Guid processDefinitionId, string? tenantId = null)
    {
        var response = await _httpClient.DeleteAsync(AddTenantQuery($"api/repository/{processDefinitionId}", tenantId));
        response.EnsureSuccessStatusCode();
    }

    public async Task RollbackProcessDefinitionAsync(string engineId, string processDefinitionId)
    {
        await Task.FromException(new NotSupportedException("Process definition rollback is not exposed by the API."));
    }

    public async Task<EngineConfiguration> GetEngineConfigurationAsync()
    {
        var capabilities = await _httpClient.GetFromJsonAsync<EngineCapabilities>("api/engine/capabilities")
            ?? throw new InvalidOperationException("The API returned no engine capabilities.");
        return new EngineConfiguration
        {
            StatusMessage = $"{capabilities.EngineType} engine; CMMN: {capabilities.SupportsCmmn}; Workers: {capabilities.SupportsWorkers}; Durable persistence: {capabilities.SupportsDurablePersistence}",
            DeploymentDelayMs = 0
        };
    }

    public Task UpdateEngineConfigurationAsync(EngineConfiguration configuration) =>
        Task.FromException(new NotSupportedException("Engine configuration updates are not exposed by the API."));

    public Task<IEnumerable<EngineConnection>> GetEngineConnectionsAsync() =>
        Task.FromException<IEnumerable<EngineConnection>>(new NotSupportedException("Engine connection management is not exposed by the API."));

    public Task AddEngineConnectionAsync(EngineConnection connection) =>
        Task.FromException(new NotSupportedException("Engine connection management is not exposed by the API."));
    public Task UpdateEngineConnectionAsync(EngineConnection connection) =>
        Task.FromException(new NotSupportedException("Engine connection management is not exposed by the API."));
    public Task RemoveEngineConnectionAsync(string connectionId) =>
        Task.FromException(new NotSupportedException("Engine connection management is not exposed by the API."));

    private sealed record RepositoryDeployRequest(string BpmnXml, string Name, string? TenantId);
    private sealed record StartProcessRequest(string ProcessDefinitionKey, IDictionary<string, object?>? Variables, string? BusinessKey, string? TenantId);
    private static string AddTenantQuery(string uri, string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? uri
            : $"{uri}{(uri.Contains('?') ? '&' : '?')}tenantId={Uri.EscapeDataString(tenantId)}";

    private sealed record UserRequest(string UserId, string? TenantId);
    private sealed record VariablesRequest(IDictionary<string, object?>? Variables, string? TenantId);
}