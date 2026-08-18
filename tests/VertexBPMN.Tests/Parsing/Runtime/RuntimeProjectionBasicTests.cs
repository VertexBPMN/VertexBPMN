using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Runtime;

public class RuntimeProjectionBasicTests
{
    private const string Simple = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:userTask id="task1">
      <bpmn:extensionElements>
        <camunda:assignee value="alice"/>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="task1"/>
    <bpmn:sequenceFlow id="f2" sourceRef="task1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void ProjectionDisabled_RuntimeNull()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            BuildRuntimeProjection = false,
            NormalizeVendorExtensions = true
        }).ParseAsync(Simple).GetAwaiter().GetResult();

        Assert.Null(model.Runtime);
    }

    [Fact]
    public void ProjectionEnabled_PopulatesNodesAndFlows()
    {
        var model = new BpmnParser(new BpmnParserOptions {
            RoundtripMode = BpmnRoundtripMode.Strict,
            BuildRuntimeProjection = true,
            NormalizeVendorExtensions = true
        }).ParseAsync(Simple).GetAwaiter().GetResult();

        Assert.NotNull(model.Runtime);
        var rt = model.Runtime!;
        Assert.Equal("p1", rt.ProcessId);

        // 3 flow nodes (start, task1, end)
        Assert.Equal(3, rt.FlowNodes.Count);
        Assert.Contains(rt.FlowNodes, n => n.Id == "task1" && n.Type == "userTask");
        Assert.DoesNotContain(rt.FlowNodes, n => string.IsNullOrEmpty(n.Id));

        // 2 flows
        Assert.Equal(2, rt.SequenceFlows.Count);
        Assert.Contains(rt.SequenceFlows, f => f.Id == "f1" && f.SourceId == "start" && f.TargetId == "task1");

        // Vendor extension subset present (assignee)
        Assert.NotNull(rt.VendorExtensions);
        Assert.True(rt.VendorExtensions!.ContainsKey("task1"));
        Assert.Contains(rt.VendorExtensions["task1"].Keys, k => k.Contains("assignee"));
    }
}