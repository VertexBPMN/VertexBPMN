using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Runtime;

public class RuntimeProjectionUserAndScriptEnhancementsTests
{
    private BpmnParser Create(bool norm = true) => new(new BpmnParserOptions {
        RoundtripMode = BpmnRoundtripMode.Strict,
        BuildRuntimeProjection = true,
        NormalizeVendorExtensions = norm
    });

    [Fact]
    public async Task ScriptTask_ResultVariable_Exposed()
    {
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="p1">
    <startEvent id="s"/>
    <scriptTask id="st" scriptFormat="C#" resultVariable="sum">
      <script><![CDATA[return 21*2;]]></script>
    </scriptTask>
    <endEvent id="e"/>
    <sequenceFlow id="f1" sourceRef="s" targetRef="st"/>
    <sequenceFlow id="f2" sourceRef="st" targetRef="e"/>
  </process>
</definitions>
""";
        var model = await Create().ParseAsync(xml);
        var rt = model.Runtime!;
        var scriptTasksProp = rt.GetType().GetProperty("ScriptTasks");
        Assert.NotNull(scriptTasksProp);
        var dict = (System.Collections.IDictionary?)scriptTasksProp!.GetValue(rt);
        Assert.NotNull(dict);
        var entry = dict!["st"];
        Assert.NotNull(entry);
        var resultVarProp = entry.GetType().GetProperty("ResultVariable");
        Assert.NotNull(resultVarProp);
        Assert.Equal("sum", (string?)resultVarProp!.GetValue(entry));
    }

    [Fact]
    public async Task UserTask_PotentialOwner_FromFormalExpression_Exposed()
    {
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="p1">
    <startEvent id="s"/>
    <userTask id="u1">
      <resourceRole xsi:type="potentialOwner" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
        <resourceAssignmentExpression>
          <formalExpression><![CDATA[${user == 'bob'}]]></formalExpression>
        </resourceAssignmentExpression>
      </resourceRole>
    </userTask>
    <endEvent id="e"/>
    <sequenceFlow id="f1" sourceRef="s" targetRef="u1"/>
    <sequenceFlow id="f2" sourceRef="u1" targetRef="e"/>
  </process>
</definitions>
""";
        var model = await Create().ParseAsync(xml);
        var rt = model.Runtime!;
        // Expect potentialOwner exposed via VendorExtensions OR new dedicated map (we check both paths):
        var ve = rt.VendorExtensions;
        Assert.NotNull(ve);
        Assert.True(ve!.ContainsKey("u1"));
        Assert.Contains(ve["u1"].Keys, k => k.Contains("potentialOwner"));
        Assert.Equal("${user == 'bob'}", ve["u1"].First(kv => kv.Key.Contains("potentialOwner")).Value);
    }

    [Fact]
    public async Task UserTask_PotentialOwner_NotLost_WhenNormalizationDisabled()
    {
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="p1">
    <startEvent id="s"/>
    <userTask id="u1">
      <resourceRole xsi:type="potentialOwner" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
        <resourceAssignmentExpression>
          <formalExpression>group:sales</formalExpression>
        </resourceAssignmentExpression>
      </resourceRole>
    </userTask>
    <endEvent id="e"/>
    <sequenceFlow id="f1" sourceRef="s" targetRef="u1"/>
    <sequenceFlow id="f2" sourceRef="u1" targetRef="e"/>
  </process>
</definitions>
""";
        var model = await Create(norm:false).ParseAsync(xml);
        var rt = model.Runtime!;
        // Even without NormalizeVendorExtensions we expect potentialOwner discoverable (either via VendorExtensions null OR still captured)
        var ve = rt.VendorExtensions;
        if (ve is not null && ve.ContainsKey("u1"))
        {
            Assert.Contains(ve["u1"].Keys, k => k.Contains("potentialOwner"));
            Assert.Equal("group:sales", ve["u1"].First(kv => kv.Key.Contains("potentialOwner")).Value);
        }
        else
        {
            // Fallback future: dedicated PotentialOwners map (if introduced)
            var potOwnersProp = rt.GetType().GetProperty("PotentialOwners");
            Assert.NotNull(potOwnersProp); // will fail RED until implemented if relying on a new structure
        }
    }
}