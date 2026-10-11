using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedPhaseGContainmentTests
{
	private readonly BpmnParser _parser = new();

	[Fact]
	public async Task Subprocess_Contains_Child_FlowNodes_And_SequenceFlows()
	{
		const string xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <subProcess id='spOuter'>
      <startEvent id='s1'/>
      <subProcess id='spInner'>
        <userTask id='t_inner'/>
      </subProcess>
      <endEvent id='e1'/>
      <sequenceFlow id='f1' sourceRef='s1' targetRef='spInner'/>
      <sequenceFlow id='f2' sourceRef='spInner' targetRef='e1'/>
    </subProcess>
  </process>
</definitions>
""";
		var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.Subprocesses);
		var spOuter = model.Subprocesses.Single(sp => sp.Id == "spOuter");
		Assert.NotNull(spOuter.ChildFlowNodeIds);
		Assert.NotNull(spOuter.ChildSequenceFlowIds);
		var outerFlowNodes = spOuter.ChildFlowNodeIds;
		var outerSequenceFlows = spOuter.ChildSequenceFlowIds;
		Assert.Contains("s1", outerFlowNodes, StringComparer.Ordinal);
		Assert.Contains("spInner", outerFlowNodes, StringComparer.Ordinal);
		Assert.Contains("e1", outerFlowNodes, StringComparer.Ordinal);
		Assert.Contains("f1", outerSequenceFlows, StringComparer.Ordinal);
		Assert.Contains("f2", outerSequenceFlows, StringComparer.Ordinal);
		var spInner = model.Subprocesses.Single(sp => sp.Id == "spInner");
		Assert.NotNull(spInner.ChildFlowNodeIds);
		var innerFlowNodes = spInner.ChildFlowNodeIds;
		Assert.Single(innerFlowNodes); // t_inner
		Assert.Contains("t_inner", innerFlowNodes, StringComparer.Ordinal);
	}
}
