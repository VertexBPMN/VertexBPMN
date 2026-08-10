using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

public class DebugApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly CustomWebApplicationFactory _factory;

    public DebugApiTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
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
    public async Task Trace_Bpmn_Process_Returns_Execution_Trace()
    {
        const string bpmn = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'/><endEvent id='end1'/><sequenceFlow id='flow1' sourceRef='start1' targetRef='end1'/></process></definitions>";
        var traceReq = new { BpmnXml = bpmn, Variables = new Dictionary<string, object>() };
        var tracePost = await _client.PostAsJsonAsync("/api/debug/trace", traceReq);
        tracePost.EnsureSuccessStatusCode();
        var trace = await tracePost.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(trace);
        Assert.Contains(trace, item => item.Contains("StartEvent: start1"));
        Assert.Contains(trace, item => item.Contains("EndEvent: end1"));
    }
}
