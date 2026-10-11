using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended;

public class C_8_0_Test
{
	// KORREKTUR: Hier fehlte das [Fact]-Attribut – der Test wurde von xUnit nie ausgeführt.
	[Fact]
	public async Task Test_C_8_0_Bpmn()
	{
		var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.8.0.bpmn");
		var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
		var logger = new Mock<ILogger<BpmnParser>>();
		// KORREKTUR: vorher `new BpmnParser()` ohne Logger/TracerProvider – inkonsistent
		// zu allen anderen Tests dieser Suite, `logger` wurde deklariert aber nie benutzt.
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		Assert.NotNull(model);
		model = model with
		{
			ProcessVariables = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["Vacation Approval"] = "Approved"
			}
		};
		var engine = new ProcessEngine();
		var result = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);
		Assert.NotNull(result);
		Assert.True(result.Count > 0, "No trace produced for C.8.0.bpmn");
		var types = result.Select(x => x.Split(':')[0].Trim()).ToList();
		Assert.Contains("StartEvent", types, StringComparer.Ordinal);
		Assert.DoesNotContain("UserTask", types, StringComparer.Ordinal);
		Assert.Contains("BusinessRuleTask", types, StringComparer.Ordinal);  // Verify the rule eval happens
		Assert.Contains("SequenceFlow", types, StringComparer.Ordinal);
		Assert.Contains("ExclusiveGateway", types, StringComparer.Ordinal);
		Assert.Contains("EndEvent", types, StringComparer.Ordinal);
		foreach (var item in result)
		{
			Console.WriteLine($"Result item: {item}");
		}
	}
}
