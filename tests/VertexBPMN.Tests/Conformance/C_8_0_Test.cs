using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Conformance;

public sealed class C_8_0_Test
{
	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849", Justification = "The synchronous engine overload accepts process input variables; no equivalent async overload exists.")]
	public async Task Test_C_8_0_Bpmn()
	{
		var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.8.0.bpmn");
		var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
		var parser = new BpmnParser();
		var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model);
		var ruleTask = Assert.Single(model.Tasks!, task => task.Type == "businessRuleTask");
		Assert.NotNull(ruleTask.Attributes);
		Assert.Equal("_9ba1b7b0-c84d-484f-9203-3792edc6dcbd", ruleTask.Attributes["zeebe:taskHeaders.decisionRef"]);
		var dmnXml = await File.ReadAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "Vacation Approval.dmn"), TestContext.Current.CancellationToken);
		var decisionLogger = new Mock<ILogger<DmnEngine>>();
		var engine = new ProcessEngine(
			NullLogger<ProcessEngine>.Instance,
			NullServiceTaskRegistry.Instance,
			dmnParser: new DmnParser(NullLogger<DmnParser>.Instance),
			dmnEngine: new DmnEngine(decisionLogger.Object));
		await engine.RegisterDmnModelAsync("_9ba1b7b0-c84d-484f-9203-3792edc6dcbd", dmnXml);
		var from = DateTimeOffset.UtcNow.AddDays(30);
		var inputs = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["From"] = from,
			["To"] = from.AddDays(1),
			["Days Remaining"] = 10,
			["name"] = "Acceptance user",
			["email"] = "acceptance@example.invalid"
		};
		var decision = await new DmnParser(NullLogger<DmnParser>.Instance).ParseAsync(dmnXml, TestContext.Current.CancellationToken);
		var decisionResult = await new DmnEngine(NullLogger<DmnEngine>.Instance).EvaluateDecisionAsync(decision, inputs, TestContext.Current.CancellationToken);
		Assert.True(Equals("Approved", decisionResult["Approval"]), System.Text.Json.JsonSerializer.Serialize(decisionResult));
		List<string> result;
		try
		{
			result = engine.Execute(model, inputs);
		}
		catch (Exception failure)
		{
			var decisionFailures = decisionLogger.Invocations.SelectMany(invocation => invocation.Arguments).OfType<Exception>();
			throw new AggregateException("BPMN execution failed; preceding DMN failures are included for diagnosis.", decisionFailures.Append(failure));
		}
		Assert.NotNull(result);
		Assert.Contains(result, item => item.StartsWith("DecisionEvaluated:", StringComparison.Ordinal));
		Assert.DoesNotContain(result, item => item.StartsWith("DecisionError:", StringComparison.Ordinal));
		Assert.Contains(result, item => item.Contains("Vacation Approval='Approved'", StringComparison.Ordinal));
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
