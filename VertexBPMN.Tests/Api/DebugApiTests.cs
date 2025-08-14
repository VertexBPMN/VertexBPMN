using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using VertexBPMN.Api;
using Xunit;

namespace VertexBPMN.Tests.Api;

public class DebugApiTests : IClassFixture<WebApplicationFactory<VertexBPMN.Api.Program>>
{
    private readonly HttpClient _client;

    public DebugApiTests(WebApplicationFactory<VertexBPMN.Api.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Trace_Bpmn_Process_Returns_Execution_Trace()
    {
        const string bpmn = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'/><endEvent id='end1'/><sequenceFlow id='flow1' sourceRef='start1' targetRef='end1'/></process></definitions>";
        var traceReq = new { BpmnXml = bpmn, Variables = new Dictionary<string, object>() };
        var tracePost = await _client.PostAsJsonAsync("/api/debug/trace", traceReq);
        tracePost.EnsureSuccessStatusCode();
        var trace = await tracePost.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(trace);
        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("EndEvent: end1", trace);
    }
}
