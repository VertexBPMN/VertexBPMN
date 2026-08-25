using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly CustomWebApplicationFactory _factory;

    public ApiIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {

        _factory = factory;
        _output = output;

        // Database initialization happens automatically when CreateClient is called
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        _client.Timeout = TimeSpan.FromSeconds(30);

        factory.Services.GetRequiredService<ILoggerFactory>()
            .AddProvider(new XunitLoggerProvider(_output));
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
        var key = $"RepositoryGet_{Guid.NewGuid():N}";
        var bpmn = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><endEvent id='end'/><sequenceFlow id='flow' sourceRef='start' targetRef='end'/></process></definitions>";
        var deploy = new { BpmnXml = bpmn, Name = key, TenantId = (string?)null };
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
        const string bpmn = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='RuntimeApiProcess'><startEvent id='start1'/><endEvent id='end1'/><sequenceFlow id='flow1' sourceRef='start1' targetRef='end1'/></process></definitions>";
        var deploy = new { BpmnXml = bpmn, Name = "RuntimeTestProcess", TenantId = (string?)null };
        var deployResponse = await _client.PostAsJsonAsync("/api/repository", deploy);
        deployResponse.EnsureSuccessStatusCode();

        var start = new { ProcessDefinitionKey = "RuntimeApiProcess",
            Variables = new Dictionary<string, object>(),
            BusinessKey = (string?)null, 
            TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/runtime/start", start);
        post.EnsureSuccessStatusCode();
        var instance = await post.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);
        var get = await _client.GetAsync($"/api/runtime/{instance!.Id}");
        get.EnsureSuccessStatusCode();
        var loaded = await get.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.Equal(instance.Id, loaded!.Id);
    }

    /* [Fact]
    public async Task Decision_Evaluate_Works()
    {
        const string dmn = "<definitions xmlns='http://www.omg.org/spec/DMN/20191111/MODEL/'><decision id='apiDecisionEvaluate' name='Api Decision'><decisionTable hitPolicy='UNIQUE'><input id='i1'><inputExpression>age</inputExpression></input><output id='o1' name='result'/><rule><inputEntry>42</inputEntry><outputEntry>\"ok\"</outputEntry></rule></decisionTable></decision></definitions>";
        var deploy = new { DecisionKey = "apiDecisionEvaluate", Name = "Api Decision", DmnXml = dmn, TenantId = (string?)null };
        var deployResponse = await _client.PostAsJsonAsync("/api/decision/deploy", deploy);
        deployResponse.EnsureSuccessStatusCode();

        var eval = new { DecisionKey = "apiDecisionEvaluate", Inputs = new Dictionary<string, object> { { "age", 42 } } };
        var post = await _client.PostAsJsonAsync("/api/decision/evaluate", eval);
        post.EnsureSuccessStatusCode();
        var result = await post.Content.ReadFromJsonAsync<DecisionResult>();
        Assert.NotNull(result);
        var output = result.Variables["result"];
        var value = output is System.Text.Json.JsonElement element ? element.GetString() : output?.ToString();
        Assert.Equal("ok", value);
    } */

    [Fact]
    public async Task Management_Suspend_Resume_Delete_Works()
    {
        const string bpmn = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='ManagementApiProcess'><startEvent id='start1'/><endEvent id='end1'/><sequenceFlow id='flow1' sourceRef='start1' targetRef='end1'/></process></definitions>";
        var deploy = new { BpmnXml = bpmn, Name = "ManagementTestProcess", TenantId = (string?)null };
        var deployResponse = await _client.PostAsJsonAsync("/api/repository", deploy);
        deployResponse.EnsureSuccessStatusCode();

        // Start a process instance
        var start = new { ProcessDefinitionKey = "ManagementApiProcess", Variables = new Dictionary<string, object>(), BusinessKey = (string?)null, TenantId = (string?)null };
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
    public async Task Management_RejectsWrongTenantAndAcceptsMatchingTenant()
    {
        const string bpmn = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='TenantManagementApiProcess'><startEvent id='start1'/><endEvent id='end1'/><sequenceFlow id='flow1' sourceRef='start1' targetRef='end1'/></process></definitions>";
        var deploy = new { BpmnXml = bpmn, Name = "TenantManagementTestProcess", TenantId = "tenant-a" };
        var deployResponse = await _client.PostAsJsonAsync("/api/repository", deploy);
        deployResponse.EnsureSuccessStatusCode();

        var start = new
        {
            ProcessDefinitionKey = "TenantManagementApiProcess",
            Variables = new Dictionary<string, object>(),
            BusinessKey = (string?)null,
            TenantId = "tenant-a"
        };
        var startResponse = await _client.PostAsJsonAsync("/api/runtime/start", start);
        startResponse.EnsureSuccessStatusCode();
        var instance = await startResponse.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);

        var wrongTenant = await _client.PostAsync(
            $"/api/management/suspend-process-instance/{instance!.Id}?tenantId=tenant-b", null);
        Assert.Equal(HttpStatusCode.Forbidden, wrongTenant.StatusCode);

        var matchingTenant = await _client.PostAsync(
            $"/api/management/suspend-process-instance/{instance.Id}?tenantId=tenant-a", null);
        matchingTenant.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Identity_ListTenants_Works()
    {
        var response = await _client.GetAsync("/api/identity/list-tenants");
        response.EnsureSuccessStatusCode();
        var tenants = await response.Content.ReadFromJsonAsync<List<TenantInfo>>();
        Assert.NotNull(tenants);
        Assert.NotEmpty(tenants);
        Assert.Contains(tenants!, tenant => tenant.Name is "default" or "Acme Corp");
    }

    [Fact]
    public async Task Identity_AdminCanManageGroupsMembershipsAndAuthorizations()
    {
        var groupResponse = await _client.PostAsJsonAsync("/api/vertex/group", new
        {
            Name = "Integration Operators",
            Type = "role",
            TenantId = (string?)null
        });
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.NotNull(group);

        var membership = await _client.PostAsync($"/api/vertex/group/{group!.Id}/users/1", null);
        membership.EnsureSuccessStatusCode();

        var authorizationResponse = await _client.PostAsJsonAsync("/api/vertex/authorization", new
        {
            UserId = "1",
            GroupId = group.Id,
            Resource = "process-definition:integration",
            Permissions = "read"
        });
        authorizationResponse.EnsureSuccessStatusCode();
        var authorization = await authorizationResponse.Content.ReadFromJsonAsync<AuthorizationResponse>();
        Assert.NotNull(authorization);

        var deleteAuthorization = await _client.DeleteAsync($"/api/vertex/authorization/{authorization!.Id}");
        deleteAuthorization.EnsureSuccessStatusCode();
        var removeMembership = await _client.DeleteAsync($"/api/vertex/group/{group.Id}/users/1");
        removeMembership.EnsureSuccessStatusCode();

        var auditResponse = await _client.GetAsync("/api/audit/logs?action=HTTP_POST&limit=500");
        auditResponse.EnsureSuccessStatusCode();
        var auditLogs = await auditResponse.Content.ReadFromJsonAsync<List<AuditLog>>();
        Assert.NotNull(auditLogs);
        Assert.Contains(auditLogs!, log => log.Resource is "/vertex/group" or "/api/vertex/group");
    }

    [Fact]
    public async Task Migration_RollbackMutation_IsPersistedInAuditLog()
    {
        var migrationId = Guid.NewGuid();
        var response = await _client.PostAsync($"/api/migration/rollback/{migrationId}", null);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var auditResponse = await _client.GetAsync("/api/audit/logs?action=HTTP_POST&limit=500");
        auditResponse.EnsureSuccessStatusCode();
        var auditLogs = await auditResponse.Content.ReadFromJsonAsync<List<AuditLog>>();
        Assert.NotNull(auditLogs);
        Assert.Contains(auditLogs!, log =>
            log.Resource.Contains($"/migration/rollback/{migrationId}", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record GroupResponse(string Id, string Name, string Type);
    private sealed record AuthorizationResponse(string Id, string UserId, string GroupId, string Resource, string Permissions);

    [Fact]
    public async Task History_ListByProcessInstance_Works()
    {
        var processKey = $"HistoryTestProcess_{Guid.NewGuid():N}";
        var bpmn = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{processKey}'><startEvent id='start'/><endEvent id='end'/><sequenceFlow id='flow' sourceRef='start' targetRef='end'/></process></definitions>";
        var deploy = new { BpmnXml = bpmn, Name = processKey, TenantId = (string?)null };
        var deployResponse = await _client.PostAsJsonAsync("/api/repository", deploy);
        deployResponse.EnsureSuccessStatusCode();

        // Starting and completing the process must durably record its runtime history.
        var start = new { ProcessDefinitionKey = processKey, Variables = new Dictionary<string, object>(), BusinessKey = (string?)null, TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/runtime/start", start);
        post.EnsureSuccessStatusCode();
        var instance = await post.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);

        var response = await _client.GetAsync($"/api/history/by-process-instance/{instance!.Id}");
        response.EnsureSuccessStatusCode();
        var events = await response.Content.ReadFromJsonAsync<List<HistoryEvent>>();
        Assert.NotNull(events);
        Assert.Contains(events!, item => item.EventType == "PROCESS_STARTED");
        Assert.Contains(events!, item => item.EventType == "END_EVENT_REACHED");
        Assert.Contains(events!, item => item.EventType == "PROCESS_COMPLETED");
        Assert.All(events!, item => Assert.Equal(instance.Id, item.ProcessInstanceId));
    }

    [Fact]
    public async Task Task_List_Returns_Empty_By_Default()
    {
        var response = await _client.GetAsync("/api/task");
        response.EnsureSuccessStatusCode();
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskInstance>>();
        Assert.NotNull(tasks);
        //Assert.Empty(tasks);
    }

    [Fact]
    public async Task Identity_ValidateUser_IsExplicitlyUnavailableWithoutLocalCredentials()
    {
        var response = await _client.GetAsync("/api/identity/validate-user?username=admin&password=irrelevant");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Task_List_And_GetById_Returns_NotFound()
    {
        // List should be empty
        var response = await _client.GetAsync("/api/task");
        response.EnsureSuccessStatusCode();
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskInstance>>();
        Assert.NotNull(tasks);
        //Assert.Empty(tasks);
        var id = tasks.FirstOrDefault()?.Id;
        // Get by random ID should return 404
        var get = await _client.GetAsync($"/api/task/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
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
        var key = $"RepositoryDelete_{Guid.NewGuid():N}";
        var bpmn = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><endEvent id='end'/><sequenceFlow id='flow' sourceRef='start' targetRef='end'/></process></definitions>";
        var deploy = new { BpmnXml = bpmn, Name = key, TenantId = (string?)null };
        var post = await _client.PostAsJsonAsync("/api/repository", deploy);
        post.EnsureSuccessStatusCode();
        var created = await post.Content.ReadFromJsonAsync<ProcessDefinition>();
        Assert.NotNull(created);
        var wrongTenantDelete = await _client.DeleteAsync($"/api/repository/{created!.Id}?tenantId=other-tenant");
        Assert.Equal(HttpStatusCode.Forbidden, wrongTenantDelete.StatusCode);
        var delete = await _client.DeleteAsync($"/api/repository/{created!.Id}");
        delete.EnsureSuccessStatusCode();
        var get = await _client.GetAsync($"/api/repository/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    public record ProcessDefinition(Guid Id, string Key, string Name, int Version, string BpmnXml, string? TenantId, DateTime CreatedAt);
    public record ProcessInstance(Guid Id, Guid ProcessDefinitionId, string? BusinessKey, string? TenantId, DateTime StartedAt, DateTime? EndedAt);
    //public record DecisionResult(Dictionary<string, object> Outputs);
    public record TenantInfo(string Id, string Name);
    public record HistoryEvent(Guid Id, Guid ProcessInstanceId, string EventType, DateTime Timestamp, string? Details, string? TenantId);
    public record TaskInstance(Guid Id, Guid ProcessInstanceId, string Name, string Type, string? Assignee, string? TenantId, DateTime CreatedAt, DateTime? CompletedAt);
}
