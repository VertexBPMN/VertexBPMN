using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended;

public class B_2_0_Test
{
	[Fact]
	public async Task Test_B_2_0_Bpmn()
	{
		var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "B.2.0.bpmn");
		var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		Assert.NotNull(model);
		var engine = new ProcessEngine();
		var result = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);
		Assert.NotNull(result);
		Assert.True(result.Count > 0, "No trace produced for B.2.0.bpmn");

		// B.2.0 validiert die deutlich umfangreichere "Analytic"-Konformitätsklasse.
		// Referenzmodell enthält u. a.: sendTask, receiveTask, serviceTask, boundaryEvent,
		// inclusiveGateway, eventBasedGateway, parallelGateway, subProcess, callActivity.
		Assert.Contains(result, r => r.Contains("StartEvent", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("EndEvent", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("UserTask", StringComparison.Ordinal));
		Assert.NotNull(model.Events);
		var foreignStartEvents = model.Events.Where(evt =>
			evt.Type == "startEvent" && evt.ProcessId != model.ProcessId).ToArray();
		Assert.NotEmpty(foreignStartEvents);
		Assert.DoesNotContain(foreignStartEvents, evt =>
			result.Any(entry => entry.Contains($"StartEvent: {evt.Id}", StringComparison.Ordinal)));
	}
}
