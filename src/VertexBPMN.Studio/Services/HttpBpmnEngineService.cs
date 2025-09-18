using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;
using System.Net.Http.Json;
using System.Text.Json;

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

    public async Task DeployAsync(IBrowserFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new("application/octet-stream");
            content.Add(fileContent, "file", file.Name);

            var response = await _httpClient.PostAsync("api/repository/deployment", content);
            response.EnsureSuccessStatusCode();
            
            OnEventEmitted?.Invoke($"Deployment created: {file.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying file {FileName}", file.Name);
            throw;
        }
    }

    public async Task<IEnumerable<Deployment>> GetDeploymentsAsync()
    {
        try
        {
            var deployments = await _httpClient.GetFromJsonAsync<IEnumerable<Deployment>>("api/repository/deployment");
            return deployments ?? Enumerable.Empty<Deployment>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching deployments");
            return Enumerable.Empty<Deployment>();
        }
    }

    public async Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync()
    {
        try
        {
            var definitions = await _httpClient.GetFromJsonAsync<IEnumerable<ProcessDefinition>>("api/repository/process-definition");
            return definitions ?? Enumerable.Empty<ProcessDefinition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process definitions");
            return Enumerable.Empty<ProcessDefinition>();
        }
    }

    public async Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync()
    {
        try
        {
            var instances = await _httpClient.GetFromJsonAsync<IEnumerable<ProcessInstance>>("api/runtime/process-instance");
            return instances ?? Enumerable.Empty<ProcessInstance>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process instances");
            return Enumerable.Empty<ProcessInstance>();
        }
    }

    public async Task<IEnumerable<UserTask>> GetTasksAsync()
    {
        try
        {
            var tasks = await _httpClient.GetFromJsonAsync<IEnumerable<UserTask>>("api/task");
            return tasks ?? Enumerable.Empty<UserTask>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tasks");
            return Enumerable.Empty<UserTask>();
        }
    }

    public async Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/repository/process-definition/{processDefinitionId}/xml");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process definition XML for {ProcessDefinitionId}", processDefinitionId);
            return string.Empty;
        }
    }

    public async Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key)
    {
        try
        {
            var versions = await _httpClient.GetFromJsonAsync<IEnumerable<ProcessDefinition>>($"api/repository/process-definition?key={key}");
            return versions ?? Enumerable.Empty<ProcessDefinition>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process definition versions for {Key}", key);
            return Enumerable.Empty<ProcessDefinition>();
        }
    }

    public async Task RollbackProcessDefinitionAsync(string engineId, string processDefinitionId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/repository/process-definition/{processDefinitionId}/rollback", null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back process definition {ProcessDefinitionId}", processDefinitionId);
            throw;
        }
    }

    public Task<EngineConfiguration> GetEngineConfigurationAsync()
    {
        return Task.FromResult(new EngineConfiguration
        {
            StatusMessage = "Connected to API",
            DeploymentDelayMs = 0
        });
    }

    public Task UpdateEngineConfigurationAsync(EngineConfiguration configuration)
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<EngineConnection>> GetEngineConnectionsAsync()
    {
        var connections = new List<EngineConnection>
        {
            new() { Id = "api", Name = "VertexBPMN API", Url = "http://localhost:5074", IsActive = true }
        };
        return Task.FromResult<IEnumerable<EngineConnection>>(connections);
    }

    public Task AddEngineConnectionAsync(EngineConnection connection) => Task.CompletedTask;
    public Task UpdateEngineConnectionAsync(EngineConnection connection) => Task.CompletedTask;
    public Task RemoveEngineConnectionAsync(string connectionId) => Task.CompletedTask;
}