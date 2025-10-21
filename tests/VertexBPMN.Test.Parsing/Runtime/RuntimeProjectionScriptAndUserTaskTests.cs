using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Runtime;

public class RuntimeProjectionScriptAndUserTaskTests
{
    private BpmnParser Create() => new(new BpmnParserOptions
    {
        RoundtripMode = BpmnRoundtripMode.Strict,
        BuildRuntimeProjection = true,
        NormalizeVendorExtensions = true
    });

    // Erwartung: RuntimeProcessModel enthält ScriptTask-Metadaten (Format + Body)
    [Fact]
    public async Task ScriptTask_Projection_ContainsScriptFormatAndBody()
    {
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <process id="p1">
    <startEvent id="start"/>
    <scriptTask id="script1" name="Run Groovy" scriptFormat="groovy">
      <script><![CDATA[
println 'hi'
]]></script>
    </scriptTask>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="script1"/>
    <sequenceFlow id="f2" sourceRef="script1" targetRef="end"/>
  </process>
</definitions>
""";
        var model = await Create().ParseAsync(xml);
        Assert.NotNull(model.Runtime);

        // RED: Erwartete neue Runtime API (ScriptTask-Details)
        // Annahme: model.Runtime!.ScriptTasks[id].ScriptFormat / ScriptBody (oder ähnlich)
        // Anpassung bei Implementierung erforderlich falls Namensgebung abweicht.
        var scriptTasksProp = model.Runtime!.GetType().GetProperty("ScriptTasks");
        Assert.NotNull(scriptTasksProp); // Sollte nach Implementierung existieren

        var scriptDict = scriptTasksProp!.GetValue(model.Runtime) as System.Collections.IDictionary;
        Assert.NotNull(scriptDict);
        Assert.True(scriptDict!.Contains("script1"));

        var details = scriptDict["script1"];
        Assert.NotNull(details);

        var formatProp = details.GetType().GetProperty("ScriptFormat");
        var bodyProp = details.GetType().GetProperty("ScriptBody");
        Assert.NotNull(formatProp);
        Assert.NotNull(bodyProp);

        Assert.Equal("groovy", (string?)formatProp!.GetValue(details));
        var body = (string?)bodyProp!.GetValue(details);
        Assert.Contains("println 'hi'", body);
    }

    // Erwartung: UserTask Vendor Extensions enthalten assignee + candidateGroups
    [Fact]
    public async Task UserTask_VendorExtensions_AssigneeAndCandidateGroupsPresent()
    {
        const string xml = """
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
             xmlns:activiti="http://activiti.org/bpmn">
  <process id="p1">
    <startEvent id="start"/>
    <userTask id="u1" name="Approve">
      <extensionElements>
        <camunda:assignee value="bob"/>
        <activiti:candidateGroups value="sales,hr"/>
      </extensionElements>
    </userTask>
    <endEvent id="end"/>
    <sequenceFlow id="f1" sourceRef="start" targetRef="u1"/>
    <sequenceFlow id="f2" sourceRef="u1" targetRef="end"/>
  </process>
</definitions>
""";
        var model = await Create().ParseAsync(xml);
        var rt = model.Runtime!;
        Assert.NotNull(rt.VendorExtensions);

        Assert.True(rt.VendorExtensions!.ContainsKey("u1"));
        var map = rt.VendorExtensions["u1"];

        // Assignee-Schlüssel (camunda) vorhanden
        Assert.Contains(map.Keys, k => k.Contains("assignee"));
        // CandidateGroups (activiti) vorhanden
        Assert.Contains(map.Keys, k => k.Contains("candidateGroups"));
        Assert.Equal("bob", map.First(kv => kv.Key.Contains("assignee")).Value);
        Assert.Equal("sales,hr", map.First(kv => kv.Key.Contains("candidateGroups")).Value);
    }
}