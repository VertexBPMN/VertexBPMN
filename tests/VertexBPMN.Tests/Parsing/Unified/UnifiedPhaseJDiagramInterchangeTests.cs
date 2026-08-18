using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedPhaseJDiagramInterchangeTests
{
    [Fact]
    public async Task Parses_Shapes_And_Edges_When_Enabled()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:bpmndi='http://www.omg.org/spec/BPMN/20100524/DI' xmlns:omgdc='http://www.omg.org/spec/DD/20100524/DC' xmlns:omgdi='http://www.omg.org/spec/DD/20100524/DI'>
  <process id='p1'>
    <startEvent id='start1'/>
    <endEvent id='end1'/>
    <sequenceFlow id='f1' sourceRef='start1' targetRef='end1'/>
  </process>
  <bpmndi:BPMNDiagram id='p1_diagram'>
    <bpmndi:BPMNPlane bpmnElement='p1'>
      <bpmndi:BPMNShape id='shape_start1' bpmnElement='start1'>
        <omgdc:Bounds x='100' y='150' width='36' height='36'/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id='shape_end1' bpmnElement='end1'>
        <omgdc:Bounds x='300' y='150' width='36' height='36'/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id='edge_f1' bpmnElement='f1'>
        <omgdi:waypoint x='136' y='168'/>
        <omgdi:waypoint x='300' y='168'/>
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</definitions>
""";
        var parser = new BpmnParser(new BpmnParserOptions { ParseDiagramInterchange = true });
        var model = await parser.ParseAsync(xml);
        Assert.NotNull(model.Shapes);
        Assert.NotNull(model.Edges);
        Assert.Equal(2, model.Shapes!.Count);
        Assert.Single(model.Edges!);
        var shape = model.Shapes!.First(s => s.BpmnElementId == "start1");
        Assert.Equal(100, shape.X);
        Assert.Equal(36, shape.Width);
        var edge = model.Edges!.First();
        Assert.Equal(2, edge.Waypoints.Count);
        Assert.Equal(136, edge.Waypoints[0].X);
    }

    [Fact]
    public async Task Serializer_Emits_DI_When_Shapes_Present()
    {
        var parser = new BpmnParser(new BpmnParserOptions { ParseDiagramInterchange = true });
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:bpmndi='http://www.omg.org/spec/BPMN/20100524/DI' xmlns:omgdc='http://www.omg.org/spec/DD/20100524/DC' xmlns:omgdi='http://www.omg.org/spec/DD/20100524/DI'>
  <process id='p2'>
    <startEvent id='s1'/>
    <endEvent id='e1'/>
    <sequenceFlow id='f1' sourceRef='s1' targetRef='e1'/>
  </process>
</definitions>
""";
        var model = await parser.ParseAsync(xml);
        // Add synthetic DI
        var updated = model with { Shapes = new [] { new BpmnShape("shape_s1","s1",10,20,30,40) }, Edges = new [] { new BpmnEdge("edge_f1","f1", new [] { (0d,0d),(10d,10d) }) } };
        var serializer = new NormalizedProjectionSerializer();
        var outXml = serializer.Serialize(updated);
        Assert.Contains("BPMNDiagram", outXml);
        Assert.Contains("waypoint", outXml);
        Assert.Contains("shape_s1", outXml);
    }
}
