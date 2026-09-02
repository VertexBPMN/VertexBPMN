using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class StrictPhase2VendorExtensionExtendedTests
{
    private const string Xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  xmlns:zeebe="http://zeebe.io/schema/zeebe/1.0"
                  xmlns:flowable="http://flowable.org/bpmn">
  <bpmn:process id="pX">
    <bpmn:userTask id="taskA" name="T">
      <bpmn:extensionElements>
        <camunda:formField id="ff1" name="Field One" type="string"/>
        <camunda:properties>
          <camunda:property name="pAlpha" value="42"/>
          <camunda:property name="emptyShouldSkip"/>
        </camunda:properties>
        <zeebe:ioMapping>
          <zeebe:input source="=inVal" target="inVar"/>
          <zeebe:output source="=calc(outVar)" target="resultVar"/>
        </zeebe:ioMapping>
        <flowable:assignee value="bob"/>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="taskA" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public async Task ExtendedVendorNormalization_Disabled_MapAbsent()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict
        }).ParseAsync(Xml, TestContext.Current.CancellationToken);

        Assert.NotNull(model.RawMetadata);
        Assert.Null(model.RawMetadata!.VendorNormalizedExtensions);
    }

    [Fact]
    public async Task ExtendedVendorNormalization_Enabled_IncludesNewKeys()
    {
        var model = await new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            NormalizeVendorExtensions = true
        }).ParseAsync(Xml, TestContext.Current.CancellationToken);

        var map = model.RawMetadata!.VendorNormalizedExtensions;
        Assert.NotNull(map);
        Assert.True(map!.ContainsKey("taskA"));

        var t = map["taskA"];
        // camunda form field
        Assert.Equal("string", t["camunda:formField.ff1.type"]);
        Assert.Equal("Field One", t["camunda:formField.ff1.name"]);
        // camunda properties
        Assert.Equal("42", t["camunda:property.pAlpha"]);
        Assert.False(t.ContainsKey("camunda:property.")); // no empty name
        // zeebe input/ output
        Assert.Equal("=inVal", t["zeebe:ioMapping.input.inVar"]);
        Assert.Equal("=calc(outVar)", t["zeebe:ioMapping.output.resultVar"]);
        // flowable assignee
        Assert.Equal("bob", t["flowable:assignee"]);
    }
}