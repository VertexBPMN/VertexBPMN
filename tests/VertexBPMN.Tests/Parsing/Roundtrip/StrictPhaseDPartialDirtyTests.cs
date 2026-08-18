using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class StrictPhaseDPartialDirtyTests
{
    private static BpmnParser P => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    // RED: partial dirty should update only targeted element without global RoundtripDirty
    [Fact]
    public void PartialDirty_TaskName_Updated_Without_Global_RoundtripDirty()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:v='http://vendor/x'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1' name='OldName'>
      <bpmn:extensionElements><v:meta a='1'/></bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:userTask id='t2' name='Keep' />
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.False(model.RawMetadata!.RoundtripDirty);
        model = BpmnRoundtripUtil.ApplyAttributeChangePartial(model, "t1", "name", "NewName");
        // Expect no global dirty flag
        Assert.False(model.RawMetadata!.RoundtripDirty);
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("id=\"t1\" name=\"NewName\"", outXml);
        Assert.Contains("id=\"t2\" name=\"Keep\"", outXml);
        // Strict still, so no dirty-roundtrip fallback diagnostic
        Assert.DoesNotContain(model.Diagnostics, d => d == "RT-Fallback:dirty-roundtrip");
    }

    // RED: dirty element should not be raw-cloned; changed attribute must override original even if present in raw
    [Fact]
    public void PartialDirty_Replaces_Raw_Name_Attribute()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1' name='Orig'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        model = BpmnRoundtripUtil.ApplyAttributeChangePartial(model, "t1", "name", "Changed");
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("name=\"Changed\"", outXml); // should be updated
    }
}
