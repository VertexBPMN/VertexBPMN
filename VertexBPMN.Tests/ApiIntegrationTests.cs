using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using VertexBPMN.Api;
using Xunit;

namespace VertexBPMN.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_Returns_OK()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Repository_Deploy_And_GetById_Works()
    {
        var deploy = new { BpmnXml = "<bpmn/>", Name = "TestProcess", TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/repository", deploy);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<ProcessDefinition>();
        Assert.NotNull(created);
        var get = await _client.GetAsync($"/api/repository/{created!.Id}");
        get.EnsureSuccessStatusCode();
        var loaded = await get.Content.ReadFromJsonAsync<ProcessDefinition>();
        Assert.Equal(created.Id, loaded!.Id);
    }

    [Fact]
    public async Task Runtime_Start_And_GetById_Works()
    {
        var start = new { ProcessDefinitionKey = "TestProcess", Variables = new Dictionary<string, object>(), BusinessKey = (string?)null, TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/runtime/start", start);
        post.EnsureSuccessStatusCode();
        var instance = await post.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);
        var get = await _client.GetAsync($"/api/runtime/{instance!.Id}");
        get.EnsureSuccessStatusCode();
        var loaded = await get.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.Equal(instance.Id, loaded!.Id);
    }

    [Fact]
    public async Task Decision_Evaluate_Works()
    {
        var eval = new { DecisionKey = "TestDecision", Inputs = new Dictionary<string, object> { { "input1", 42 } } };
        var post = await _client.PostAsJsonAsync("/api/decision/evaluate", eval);
        post.EnsureSuccessStatusCode();
        var result = await post.Content.ReadFromJsonAsync<DecisionResult>();
        Assert.NotNull(result);
        Assert.True(result.Outputs.ContainsKey("input1"));
        // System.Text.Json returns JsonElement for object values, so extract the value
        var element = (System.Text.Json.JsonElement)result.Outputs["input1"];
        Assert.Equal(42, element.GetInt32());
    }

    [Fact]
    public async Task Management_Suspend_Resume_Delete_Works()
    {
        // Start a process instance
        var start = new { ProcessDefinitionKey = "TestProcess", Variables = new Dictionary<string, object>(), BusinessKey = (string?)null, TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/runtime/start", start);
        post.EnsureSuccessStatusCode();
        var instance = await post.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);

        // Suspend
        var suspend = await _client.PostAsync($"/api/management/suspend-process-instance/{instance!.Id}", null);
        suspend.EnsureSuccessStatusCode();

        // Resume
        var resume = await _client.PostAsync($"/api/management/resume-process-instance/{instance.Id}", null);
        resume.EnsureSuccessStatusCode();

        // Delete
        var delete = await _client.PostAsync($"/api/management/delete-process-instance/{instance.Id}", null);
        delete.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Identity_ListTenants_Works()
    {
        var response = await _client.GetAsync("/api/identity/list-tenants");
        response.EnsureSuccessStatusCode();
        var tenants = await response.Content.ReadFromJsonAsync<List<TenantInfo>>();
        Assert.NotNull(tenants);
        Assert.NotEmpty(tenants);
        Assert.Equal("default", tenants![0].Name);
    }

    [Fact]
    public async Task History_ListByProcessInstance_Works()
    {
        // Start a process instance (no history events will exist in-memory, but endpoint should return 200 and an empty list)
        var start = new { ProcessDefinitionKey = "TestProcess", Variables = new Dictionary<string, object>(), BusinessKey = (string?)null, TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/runtime/start", start);
        post.EnsureSuccessStatusCode();
        var instance = await post.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);

        var response = await _client.GetAsync($"/api/history/by-process-instance/{instance!.Id}");
        response.EnsureSuccessStatusCode();
        var events = await response.Content.ReadFromJsonAsync<List<HistoryEvent>>();
        Assert.NotNull(events);
        Assert.Empty(events);
    }

    [Fact]
    public async Task Task_List_Returns_Empty_By_Default()
    {
        var response = await _client.GetAsync("/api/task");
        response.EnsureSuccessStatusCode();
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskInstance>>();
        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task Identity_ValidateUser_Works()
    {
        var response = await _client.GetAsync("/api/identity/validate-user?username=admin&password=irrelevant");
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserInfo>();
        Assert.NotNull(user);
        Assert.Equal("admin", user!.UserName);
    }

    [Fact]
    public async Task Task_List_And_GetById_Returns_NotFound()
    {
        // List should be empty
        var response = await _client.GetAsync("/api/task");
        response.EnsureSuccessStatusCode();
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskInstance>>();
        Assert.NotNull(tasks);
        Assert.Empty(tasks);

        // Get by random ID should return 404
        var get = await _client.GetAsync($"/api/task/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task History_GetById_Returns_NotFound()
    {
        var get = await _client.GetAsync($"/api/history/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Management_GetMetrics_Works()
    {
        var response = await _client.GetAsync("/api/management/metrics");
        response.EnsureSuccessStatusCode();
        var metrics = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(metrics);
    }

    [Fact]
    public async Task Decision_GetDecisionByKey_Returns_Null()
    {
        var get = await _client.GetAsync($"/api/decision/by-key?decisionKey=unknown");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Repository_Delete_Works()
    {
        var deploy = new { BpmnXml = "<bpmn/>", Name = "DeleteProcess", TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/repository", deploy);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<ProcessDefinition>();
        Assert.NotNull(created);
        var delete = await _client.DeleteAsync($"/api/repository/{created!.Id}");
        delete.EnsureSuccessStatusCode();
        var get = await _client.GetAsync($"/api/repository/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    public record ProcessDefinition(Guid Id, string Key, string Name, int Version, string BpmnXml, string? TenantId, DateTime CreatedAt);
    public record ProcessInstance(Guid Id, Guid ProcessDefinitionId, string? BusinessKey, string? TenantId, DateTime StartedAt, DateTime? EndedAt);
    public record DecisionResult(Dictionary<string, object> Outputs);
    public record TenantInfo(string Id, string Name);
    public record HistoryEvent(Guid Id, Guid ProcessInstanceId, string EventType, DateTime Timestamp, string? Details, string? TenantId);
    public record TaskInstance(Guid Id, Guid ProcessInstanceId, string Name, string Type, string? Assignee, string? TenantId, DateTime CreatedAt, DateTime? CompletedAt);
    public record UserInfo(string Id, string UserName, string Email);
}
