using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Integration.Bpmn;

public class TokenEngineGatewayTests
{
	[Fact]
	public async Task Executes_EventBasedGateway_FlowsToFirstEvent()
	{
		const string xml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'/><eventBasedGateway id='eg1'/><intermediateCatchEvent id='msg1'><messageEventDefinition/></intermediateCatchEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='eg1'/><sequenceFlow id='f2' sourceRef='eg1' targetRef='msg1'/><sequenceFlow id='f3' sourceRef='msg1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		var engine = new ProcessEngine();
		var trace = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);
		Assert.Contains(trace, entry => entry.Contains("StartEvent: start1", StringComparison.Ordinal));
		Assert.Contains(trace, entry => entry.Contains("SequenceFlow: f1", StringComparison.Ordinal));
		Assert.Contains(trace, entry => entry.Contains("SequenceFlow: f2", StringComparison.Ordinal));
		Assert.Contains(trace, entry => entry.Contains("SequenceFlow: f3", StringComparison.Ordinal));
		Assert.Contains(trace, entry => entry.Contains("EndEvent: end1", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Executes_ComplexGateway_FlowsToNext()
	{
		const string xml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P2'><startEvent id='start1'/><complexGateway id='cg1'/><sequenceFlow id='f1' sourceRef='start1' targetRef='cg1'/><sequenceFlow id='f2' sourceRef='cg1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
		var logger = new Mock<ILogger<BpmnParser>>();
		var parser = new BpmnParser(logger.Object, TracerProvider.Default);
		var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
		var engine = new ProcessEngine();
		var trace = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);
		Assert.Contains(trace, entry => entry.Contains("StartEvent: start1", StringComparison.Ordinal));
		Assert.Contains(trace, entry => entry.Contains("SequenceFlow: f1", StringComparison.Ordinal));
		Assert.Contains(trace, entry => entry.Contains("SequenceFlow: f2", StringComparison.Ordinal));
		Assert.Contains(trace, entry => entry.Contains("EndEvent: end1", StringComparison.Ordinal));
	}
}
