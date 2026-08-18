using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class StrictPhaseBAdditionalValidationTests
{
    private static BpmnParser P => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void Terminate_End_Outside_Transaction_Produces_Diagnostic()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <endEvent id='end_t'>
      <terminateEventDefinition />
    </endEvent>
  </process>
</definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.Contains(model.Diagnostics, d => d.Contains("Terminate end event end_t outside transaction"));
    }

    [Fact]
    public void Boundary_Compensation_Default_CancelActivity_Produces_Diagnostic()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <userTask id='t1'/>
    <boundaryEvent id='bComp' attachedToRef='t1'>
      <compensateEventDefinition />
    </boundaryEvent>
  </process>
</definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.Contains(model.Diagnostics, d => d.Contains("Boundary compensation event bComp must have cancelActivity='false'"));
    }

    [Fact]
    public void Boundary_Compensation_With_CancelActivity_False_Is_Valid()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <userTask id='t1'/>
    <boundaryEvent id='bComp' attachedToRef='t1' cancelActivity='false'>
      <compensateEventDefinition />
    </boundaryEvent>
  </process>
</definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.DoesNotContain(model.Diagnostics, d => d.Contains("Boundary compensation event bComp"));
    }
}
