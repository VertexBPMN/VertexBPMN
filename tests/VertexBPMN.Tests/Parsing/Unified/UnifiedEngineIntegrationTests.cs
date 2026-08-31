using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedEngineIntegrationTests
{
    private BpmnParser CreateParser() => new(new BpmnParserOptions());

    [Fact]
    public async Task Mapper_Builds_Basic_Graph_Metadata()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <startEvent id='s1'/>
    <userTask id='task1'/>
    <endEvent id='e1'/>
    <sequenceFlow id='f1' sourceRef='s1' targetRef='task1'/>
    <sequenceFlow id='f2' sourceRef='task1' targetRef='e1'/>
  </process>
</definitions>
""";
        var parser = CreateParser();
        var unified = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var mapper = new EngineMapper();
        var result = mapper.Map("p1", unified);
        Assert.NotNull(result.ProcessDefinition);
        var def = result.ProcessDefinition!;
        Assert.Equal(3, def.Nodes.Count);
        Assert.Equal(2, def.SequenceFlows.Count);
        Assert.Single(def.StartEventIds);
        Assert.True(def.Outgoing.ContainsKey("s1"));
        Assert.True(def.Incoming.ContainsKey("task1"));
        Assert.DoesNotContain(result.MappingDiagnostics, d =>d.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Runtime_Starts_Process_And_Creates_UserTask()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='demo'>
    <startEvent id='start'/>
    <userTask id='approve'/>
    <endEvent id='end'/>
    <sequenceFlow id='f1' sourceRef='start' targetRef='approve'/>
    <sequenceFlow id='f2' sourceRef='approve' targetRef='end'/>
  </process>
</definitions>
""";
        var parser = CreateParser();
        var unified = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var mapper = new EngineMapper();
        var mapped = mapper.Map("demo", unified).ProcessDefinition!;
        var runtime = new EngineRuntime();
        runtime.Deploy(mapped);
        var start = runtime.Start("demo");
        Assert.Single(start.ActivatedTasks);
        var task = start.ActivatedTasks.Single();
        var completion = runtime.CompleteUserTask(start.Instance.Id, task.Id);
        Assert.True(completion.ProcessCompleted); // straight-through
        Assert.True(completion.Instance.Completed);
    }

    [Fact]
    public async Task Runtime_Chooses_Second_Flow_When_First_Condition_False()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:vertex='http://vertexbpmn.io/schema/1.0'>
  <process id='route'>
    <startEvent id='s'/>
    <exclusiveGateway id='g1'/>
    <userTask id='t_true'/>
    <userTask id='t_fallback'/>
    <endEvent id='e'/>
    <sequenceFlow id='f_s_g' sourceRef='s' targetRef='g1'/>
    <sequenceFlow id='f1' sourceRef='g1' targetRef='t_true' vertex:priority='10'>
      <conditionExpression>${x}</conditionExpression>
    </sequenceFlow>
    <sequenceFlow id='f2' sourceRef='g1' targetRef='t_fallback' vertex:priority='5'/>
    <sequenceFlow id='f3' sourceRef='t_true' targetRef='e'/>
    <sequenceFlow id='f4' sourceRef='t_fallback' targetRef='e'/>
  </process>
</definitions>
""";
        var parser = CreateParser();
        var unified = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var mapper = new EngineMapper();
        var def = mapper.Map("route", unified).ProcessDefinition!;
        var runtime = new EngineRuntime();
        runtime.Deploy(def);
        // Variable x = false -> first conditional path skipped
        var start = runtime.Start("route", new Dictionary<string, object?> { ["x"] = false });
        var open = start.ActivatedTasks;
        Assert.Single(open);
        Assert.Equal("t_fallback", open.Single().NodeId);
        var completion = runtime.CompleteUserTask(start.Instance.Id, open.Single().Id);
        Assert.True(completion.ProcessCompleted);
    }

    [Fact]
    public void Runtime_Start_Undeployed_Throws()
    {
        var runtime = new EngineRuntime();
        Assert.Throws<InvalidOperationException>(() => runtime.Start("missing"));
    }
}
