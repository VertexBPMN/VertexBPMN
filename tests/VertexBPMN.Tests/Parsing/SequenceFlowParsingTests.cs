using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing;

public class SequenceFlowParsingTests
{
    private readonly BpmnParser _parser = new();

    [Fact]
    public async Task Parses_Condition_And_Default_Flow()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <exclusiveGateway id='g1' default='flow2'/>
    <sequenceFlow id='flow1' sourceRef='g1' targetRef='taskA'>
      <conditionExpression><![CDATA[${x > 5}]]></conditionExpression>
    </sequenceFlow>
    <sequenceFlow id='flow2' sourceRef='g1' targetRef='taskB'/>
    <userTask id='taskA'/>
    <userTask id='taskB'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        Assert.Equal(2, model.SequenceFlows.Count);
        var cond = model.SequenceFlows.First(f => f.Id == "flow1");
        Assert.Equal("${x > 5}", cond.ConditionExpression);
        var def = model.SequenceFlows.First(f => f.Id == "flow2");
        Assert.True(def.IsDefault);
    }

    [Fact]
    public async Task Preserves_Condition_Expression_Language_Override_And_Model_Default()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'
             expressionLanguage='https://www.omg.org/spec/DMN/20191111/FEEL/'>
  <process id='p1'>
    <sequenceFlow id='feel' sourceRef='start' targetRef='taskA'>
      <conditionExpression>approved</conditionExpression>
    </sequenceFlow>
    <sequenceFlow id='xpath' sourceRef='start' targetRef='taskB'>
      <conditionExpression language='http://www.w3.org/1999/XPath'>${approved}</conditionExpression>
    </sequenceFlow>
  </process>
</definitions>
""";

        var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://www.omg.org/spec/DMN/20191111/FEEL/",
            model.SequenceFlows.Single(flow => flow.Id == "feel").ConditionExpressionLanguage);
        Assert.Equal(
            "http://www.w3.org/1999/XPath",
            model.SequenceFlows.Single(flow => flow.Id == "xpath").ConditionExpressionLanguage);
    }

    [Fact]
    public async Task Preserves_Owning_Process_For_Collaboration_Flow_Nodes()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='primary'>
    <startEvent id='primary-start'/>
    <sequenceFlow id='primary-flow' sourceRef='primary-start' targetRef='primary-task'/>
    <task id='primary-task'/>
  </process>
  <process id='secondary'>
    <startEvent id='secondary-start'/>
    <sequenceFlow id='secondary-flow' sourceRef='secondary-start' targetRef='secondary-task'/>
    <task id='secondary-task'/>
  </process>
</definitions>
""";

        var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);

        Assert.Equal("primary", model.Events.Single(evt => evt.Id == "primary-start").ProcessId);
        Assert.Equal("secondary", model.Events.Single(evt => evt.Id == "secondary-start").ProcessId);
        Assert.Equal("secondary", model.Tasks.Single(task => task.Id == "secondary-task").ProcessId);
        Assert.Equal("secondary", model.SequenceFlows.Single(flow => flow.Id == "secondary-flow").ProcessId);
    }
}
