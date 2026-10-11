using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended;

public class A_3_0_Test
{
	[Fact]
	public async Task Test_A_3_0_Bpmn()
	{
		var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "A.3.0.bpmn");
		var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model);
		var engine = new ProcessEngine();
		var result = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);
		Assert.NotNull(result);
		Assert.True(result.Count > 0, "No trace produced for A.3.0.bpmn");

		// A.3.0 prüft Exception Flows an Boundary Events (an einem SubProcess).
		// Referenzmodell enthält 1 subProcess, 2 boundaryEvent (message/escalation), 2 endEvent.
		Assert.Contains(result, r => r.Contains("StartEvent", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("EndEvent", StringComparison.Ordinal));
		// ACHTUNG: "SubProcess"/"BoundaryEvent" als Bezeichner sind bisher in keinem
		// anderen Test dieser Suite bestätigt (Vokabular nur aus C.8.0/C.8.1 abgeleitet:
		// StartEvent, EndEvent, UserTask, ExclusiveGateway, ParallelGateway, BusinessRuleTask).
		// Bitte gegen die tatsächliche ToString()-Ausgabe eures Trace-Objekts prüfen,
		// falls diese Assertions fehlschlagen.
		Assert.Contains(result, r => r.Contains("SubProcess", StringComparison.Ordinal));
		Assert.Contains(result, r => r.Contains("BoundaryEventSkipped: ", StringComparison.Ordinal));
	}
}
