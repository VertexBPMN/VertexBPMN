using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended;

public class C_1_1_Test
{
	[Fact]
	public async Task Test_C_1_1_Bpmn()
	{
		var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.1.1.bpmn");
		var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		Assert.NotNull(model);
		model = model with
		{
			ProcessVariables = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["approved"] = true,
				["clarified"] = "yes"
			}
		};
		var engine = new ProcessEngine();
		var result = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);
		Assert.NotNull(result);
		Assert.True(result.Count > 0, "No trace produced for C.1.1.bpmn");
		Assert.Contains(result, r => r.Contains("StartEvent", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("EndEvent", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("UserTask", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("ExclusiveGateway", StringComparison.Ordinal));
	}
}
