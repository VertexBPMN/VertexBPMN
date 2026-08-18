using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class StrictPhase2VendorExtensionTests
{
    private const string Xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  xmlns:zeebe="http://zeebe.io/schema/zeebe/1.0"
                  xmlns:mcp="http://vertexbpmn.io/mcp">
  <bpmn:process id="p1">
    <bpmn:userTask id="ut1" name="Work">
      <bpmn:extensionElements>
        <camunda:assignee value="alice"/>
        <zeebe:taskDefinition type="workerA"/>
        <zeebe:ioMapping>
          <zeebe:input source="=x" target="varX"/>
        </zeebe:ioMapping>
        <!-- FIX: escape inner quotes inside JSON -->
        <mcp:mcpServiceTask mcpServerUrl="http://api" mcpMethod="Do" mcpParams="{&quot;a&quot;:1}" />
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="ut1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void VendorNormalization_Disabled_ByDefault()
    {
        var model = new BpmnParser(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict })
            .ParseAsync(Xml).GetAwaiter().GetResult();

        Assert.NotNull(model.RawMetadata);
        Assert.True(model.RawMetadata!.RawExtensionElements?.ContainsKey("ut1") == true);
        Assert.Null(model.RawMetadata!.VendorNormalizedExtensions);
    }

    [Fact]
    public void VendorNormalization_Enabled_FlattensKnownVendors()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            NormalizeVendorExtensions = true
        }).ParseAsync(Xml).GetAwaiter().GetResult();

        Assert.NotNull(model.RawMetadata);
        var map = model.RawMetadata!.VendorNormalizedExtensions;
        Assert.NotNull(map);
        Assert.True(map!.ContainsKey("ut1"));

        var ut = map["ut1"];
        Assert.Equal("alice", ut["camunda:assignee"]);
        Assert.Equal("workerA", ut["zeebe:taskDefinition.type"]);
        Assert.Equal("=x", ut["zeebe:ioMapping.input.varX"]);
        Assert.Equal("http://api", ut["mcp:mcpServiceTask.mcpServerUrl"]);
        Assert.Equal("Do", ut["mcp:mcpServiceTask.mcpMethod"]);
        Assert.Equal(@"{""a"":1}", ut["mcp:mcpServiceTask.mcpParams"]);
        Assert.True(model.RawMetadata!.RawExtensionElements?.ContainsKey("ut1") == true);
    }
}