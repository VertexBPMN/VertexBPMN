using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class StrictPhaseDMutationsTests
{
    private static BpmnParser StrictParser() => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void MarkDirtyOnAnyChange_Sets_RoundtripDirty_And_Diagnostic()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        Assert.False(model.RawMetadata!.RoundtripDirty);
        model = BpmnRoundtripUtil.MarkDirtyOnAnyChange(model, "t1");
        Assert.True(model.RawMetadata!.RoundtripDirty);
        Assert.Contains(model.Diagnostics, d => d == "RT-Dirty:element:t1");
    }

    [Fact]
    public void ApplyAttributeChange_Task_Name_Updates_Model_And_Sets_Dirty()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1' name='Orig'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        model = BpmnRoundtripUtil.ApplyAttributeChange(model, "t1", "name", "Changed");
        Assert.True(model.RawMetadata!.RoundtripDirty);
        var task = model.Tasks.Single(t => t.Id == "t1");
        Assert.Equal("Changed", task.Attributes!["name"]);
        Assert.Contains(model.Diagnostics, d => d == "RT-Dirty:element:t1");
    }

    [Fact]
    public void StrictSerializer_Adds_Dirty_Fallback_Diagnostic_When_RoundtripDirty()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1' name='Orig'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        model = BpmnRoundtripUtil.ApplyAttributeChange(model, "t1", "name", "Changed");
        var serializer = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict };
        _ = serializer.Serialize(model);
        Assert.Contains(model.Diagnostics, d => d == "RT-Fallback:dirty-roundtrip");
    }
}
