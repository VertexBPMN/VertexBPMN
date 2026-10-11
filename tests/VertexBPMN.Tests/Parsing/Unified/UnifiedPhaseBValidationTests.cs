using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedPhaseBValidationTests
{
	private readonly BpmnParser _parser = new();

	[Fact]
	public async Task Detects_MultiInstance_Conflict()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn'>
  <process id='p1'>
    <subProcess id='sp1'>
      <multiInstanceLoopCharacteristics camunda:collection='items'>
        <loopCardinality>5</loopCardinality>
      </multiInstanceLoopCharacteristics>
    </subProcess>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Diagnostics);
		Assert.Contains(model.Diagnostics, d => d.Contains("multi-instance", StringComparison.Ordinal) && d.Contains("sp1", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Cancel_End_Outside_Transaction_Flagged()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <endEvent id='e1'>
      <cancelEventDefinition />
    </endEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Diagnostics);
		Assert.Contains(model.Diagnostics, d => d.Contains("Cancel end event", StringComparison.Ordinal) && d.Contains("e1", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Gateway_With_No_Outgoing_Flagged()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <exclusiveGateway id='g1'/>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Diagnostics);
		Assert.Contains(model.Diagnostics, d => d.Contains("Gateway g1 has no outgoing", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Multiple_Link_Throw_Flagged()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <intermediateThrowEvent id='t1'>
      <linkEventDefinition name='L1'/>
    </intermediateThrowEvent>
    <intermediateThrowEvent id='t2'>
      <linkEventDefinition name='L1'/>
    </intermediateThrowEvent>
    <intermediateCatchEvent id='c1'>
      <linkEventDefinition name='L1'/>
    </intermediateCatchEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Diagnostics);
		Assert.Contains(model.Diagnostics, d => d.Contains("Multiple throw link events", StringComparison.Ordinal) && d.Contains("L1", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Missing_Link_Catch_Flagged()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <intermediateThrowEvent id='t1'>
      <linkEventDefinition name='L2'/>
    </intermediateThrowEvent>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Diagnostics);
		Assert.Contains(model.Diagnostics, d => d.Contains("Unmatched link", StringComparison.Ordinal) && d.Contains("L2", StringComparison.Ordinal));
	}
}
