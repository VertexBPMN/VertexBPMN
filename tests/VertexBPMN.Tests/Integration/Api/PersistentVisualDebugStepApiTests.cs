using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class PersistentVisualDebugStepApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PersistentVisualDebugStepApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task StepOperationPersistsTokenAndInstanceStateAcrossRequests()
    {
        var processInstanceId = await SeedProcessAsync();
        using var client = _factory.CreateClient();

        var first = await client.PostAsync($"/api/visual-debugger/instance/{processInstanceId}/step", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<StepResponse>();
        Assert.NotNull(firstBody);
        Assert.Equal("startEvent", firstBody!.StartActivityId);
        Assert.Equal("approvalTask", firstBody.EndActivityId);
        Assert.False(firstBody.ProcessCompleted);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            var instance = await db.ProcessInstances.SingleAsync(value => value.Id == processInstanceId);
            var token = await db.ExecutionTokens.SingleAsync(value => value.ProcessInstanceId == processInstanceId);
            Assert.Equal("approvalTask", instance.State);
            Assert.Equal("approvalTask", token.CurrentNodeId);
            Assert.Equal("Active", token.State);
            Assert.Contains(await db.HistoryEvents.Where(value => value.ProcessInstanceId == processInstanceId).ToListAsync(),
                value => value.EventType == "VISUAL_DEBUG_STEP_OVER" && value.ElementId == "approvalTask");
        }

        var second = await client.PostAsync($"/api/visual-debugger/instance/{processInstanceId}/step", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<StepResponse>();
        Assert.NotNull(secondBody);
        Assert.Equal("approvalTask", secondBody!.StartActivityId);
        Assert.Equal("endEvent", secondBody.EndActivityId);
        Assert.True(secondBody.ProcessCompleted);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            var instance = await db.ProcessInstances.SingleAsync(value => value.Id == processInstanceId);
            var token = await db.ExecutionTokens.SingleAsync(value => value.ProcessInstanceId == processInstanceId);
            Assert.Equal(ProcessInstanceStatus.Completed, instance.Status);
            Assert.Equal("Completed", token.State);
            Assert.Empty(instance.ActiveTokens);
        }
    }

    [Fact]
    public async Task ProcessVisualizationReadsPersistedDefinitionTokensAndHistory()
    {
        var processInstanceId = await SeedProcessAsync();
        using var client = _factory.CreateClient();

        var initial = await client.GetAsync($"/api/visual-debug/visualize/{processInstanceId}");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var initialBody = await initial.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("approvalTask", initialBody.GetProperty("bpmnXml").GetString());
        Assert.Equal("startEvent", initialBody.GetProperty("activeTokens")[0].GetProperty("activityId").GetString());
        Assert.Empty(initialBody.GetProperty("completedActivities").EnumerateArray());
        Assert.Equal(3, initialBody.GetProperty("metrics").GetProperty("totalActivities").GetInt32());

        var firstStep = await client.PostAsync($"/api/visual-debugger/instance/{processInstanceId}/step", null);
        Assert.Equal(HttpStatusCode.OK, firstStep.StatusCode);

        var afterFirstStep = await client.GetFromJsonAsync<JsonElement>($"/api/visual-debug/visualize/{processInstanceId}");
        Assert.Equal("approvalTask", afterFirstStep.GetProperty("activeTokens")[0].GetProperty("activityId").GetString());
        Assert.Contains(afterFirstStep.GetProperty("completedActivities").EnumerateArray(), value =>
            value.GetProperty("activityId").GetString() == "startEvent");
        Assert.Equal(1, afterFirstStep.GetProperty("metrics").GetProperty("completedActivities").GetInt32());
        Assert.Equal(1, afterFirstStep.GetProperty("metrics").GetProperty("activeActivities").GetInt32());

        var secondStep = await client.PostAsync($"/api/visual-debugger/instance/{processInstanceId}/step", null);
        Assert.Equal(HttpStatusCode.OK, secondStep.StatusCode);

        var completed = await client.GetFromJsonAsync<JsonElement>($"/api/visual-debug/visualize/{processInstanceId}");
        Assert.Empty(completed.GetProperty("activeTokens").EnumerateArray());
        Assert.Contains(completed.GetProperty("completedActivities").EnumerateArray(), value =>
            value.GetProperty("activityId").GetString() == "approvalTask");
        Assert.Equal(3, completed.GetProperty("metrics").GetProperty("completedActivities").GetInt32());
        Assert.Equal(0, completed.GetProperty("metrics").GetProperty("activeActivities").GetInt32());
    }

    [Fact]
    public async Task ProcessVisualizationEnforcesTenantIsolation()
    {
        var processInstanceId = await SeedProcessAsync("tenant-a");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "tenant-reader");
        client.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-b");

        var response = await client.GetAsync($"/api/visual-debug/visualize/{processInstanceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StepOperationEnforcesTenantIsolation()
    {
        var processInstanceId = await SeedProcessAsync("tenant-a");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "tenant-reader");
        client.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-b");

        var response = await client.PostAsync($"/api/visual-debugger/instance/{processInstanceId}/step", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> SeedProcessAsync(string? tenantId = null)
    {
        await _factory.InitializeAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = $"PersistentStepProcess-{Guid.NewGuid():N}",
            Name = "Persistent Step Process",
            Version = 1,
            TenantId = tenantId,
            DeploymentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = DateTime.UtcNow,
            BpmnXml = """
                <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
                  <process id="persistent-step-process">
                    <startEvent id="startEvent" />
                    <task id="approvalTask" name="Approval" />
                    <endEvent id="endEvent" />
                    <sequenceFlow id="flow-start" sourceRef="startEvent" targetRef="approvalTask" />
                    <sequenceFlow id="flow-end" sourceRef="approvalTask" targetRef="endEvent" />
                  </process>
                </definitions>
                """
        };
        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = definition.Id,
            TenantId = tenantId,
            State = "Running",
            ProcessId = definition.Key,
            InstanceId = Guid.NewGuid().ToString("N"),
            Status = ProcessInstanceStatus.Running,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            Variables = new Dictionary<string, object>()
        };
        var token = new ExecutionToken
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            CurrentNodeId = "startEvent",
            NodeType = "startEvent",
            State = "Active",
            CreatedAt = DateTime.UtcNow,
            Variables = new Dictionary<string, object>()
        };

        db.ProcessDefinitions.Add(definition);
        db.ProcessInstances.Add(instance);
        db.ExecutionTokens.Add(token);
        await db.SaveChangesAsync();
        return instance.Id;
    }

    private sealed record StepResponse(
        Guid ProcessInstanceId,
        Guid TokenId,
        string StartActivityId,
        string EndActivityId,
        string EndNodeType,
        bool ProcessCompleted,
        DateTime Timestamp,
        ProcessInstance Instance);
}
