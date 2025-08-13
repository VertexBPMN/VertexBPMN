using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using VertexBPMN.Api;
using Xunit;

namespace VertexBPMN.Tests.Api;

public class TimerJobApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TimerJobApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Deploy_Bpmn_With_TimerEvent_Creates_And_Executes_Job()
    {
        // BPMN: StartEvent with timer (simulate timer job creation)
        const string bpmn = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P4'><startEvent id='start1'><timerEventDefinition/></startEvent><userTask id='t1'/><endEvent id='end1'/><sequenceFlow id='f1' sourceRef='start1' targetRef='t1'/><sequenceFlow id='f2' sourceRef='t1' targetRef='end1'/></process></definitions>";
        var deployBpmn = new { bpmnXml = bpmn, name = "TimerProcess", tenantId = (string?)null };
        var bpmnPost = await _client.PostAsJsonAsync("/api/repository", deployBpmn);
        bpmnPost.EnsureSuccessStatusCode();
        var deployed = await bpmnPost.Content.ReadFromJsonAsync<ProcessDefinition>();
        // API test temporarily excluded for MIWG test run
        Assert.Equal("P4", deployed.Key);

        // Start process instance (should create timer job)
        var start = new {
            ProcessDefinitionKey = "P4",
            Variables = new Dictionary<string, object>(),
            BusinessKey = (string?)null,
            TenantId = (string?)null
        };
        var execPost = await _client.PostAsJsonAsync("/api/runtime/start", start);
        execPost.EnsureSuccessStatusCode();
        var instance = await execPost.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);
        Assert.Equal(deployed.Id, instance.ProcessDefinitionId);

        // Wait for job executor to process the timer job (simulate short wait)
        await Task.Delay(1500);
        // TODO: Query API or DB for job completion/next step (userTask available)
        // This is a placeholder for a real job state assertion
    }

    public record ProcessDefinition(Guid Id, string Key, string Name);
    public record ProcessInstance(Guid Id, Guid ProcessDefinitionId);
}
