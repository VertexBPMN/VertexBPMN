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

	public DebugApiTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
	{
		_output = output;

		// Database initialization happens automatically when CreateClient is called
		_client = factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("http://localhost")
		});

		_client.Timeout = TimeSpan.FromSeconds(30);

		XunitLoggerProvider.Register(factory.Services.GetRequiredService<ILoggerFactory>(), _output);
	}

	[Fact]
	public async Task Trace_Bpmn_Process_Returns_Execution_Trace()
	{
		const string bpmn = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'/><endEvent id='end1'/><sequenceFlow id='flow1' sourceRef='start1' targetRef='end1'/></process></definitions>";
		var traceReq = new { BpmnXml = bpmn, Variables = new Dictionary<string, object>(StringComparer.Ordinal) };
		var tracePost = await _client.PostAsJsonAsync("/api/debug/trace", traceReq, cancellationToken: TestContext.Current.CancellationToken);
		tracePost.EnsureSuccessStatusCode();
		var trace = await tracePost.Content.ReadFromJsonAsync<List<string>>(cancellationToken: TestContext.Current.CancellationToken);
		Assert.NotNull(trace);
		Assert.Contains(trace, item => item.Contains("StartEvent: start1", StringComparison.Ordinal));
		Assert.Contains(trace, item => item.Contains("EndEvent: end1", StringComparison.Ordinal));
	}
}
