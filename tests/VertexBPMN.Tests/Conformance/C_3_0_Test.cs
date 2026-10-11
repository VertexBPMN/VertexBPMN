using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Conformance;

public class C_3_0_Test
{
	[Fact]
	public async Task Test_C_3_0_Bpmn()
	{
		var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.3.0.bpmn");
		var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		Assert.NotNull(model);
		var engine = new ProcessEngine();
		Assert.NotNull(model.Events);
		var startEvent = model.Events.First(evt =>
			evt.Type == "startEvent" && evt.SubprocessId is null && evt.ProcessId == model.ProcessId);
		var result = engine.ExecuteFromStartEvent(model, startEvent.Id);
		Assert.NotNull(result);
		Assert.True(result.Count > 0, "No trace produced for C.3.0.bpmn");
		Assert.Contains(result, r => r.Contains("StartEvent", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("EndEvent", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("UserTask", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("ExclusiveGateway", StringComparison.Ordinal));
	}
}
