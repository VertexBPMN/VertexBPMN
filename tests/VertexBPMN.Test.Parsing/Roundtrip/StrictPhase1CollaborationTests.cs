using VertexBPMN.Engine.Parsing;
using VertexBPMN.Domain.Model.Bpmn;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhase1CollaborationTests
{
    private const string XmlWithCollab = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
  <bpmn:message id="msg1" name="M1"/>
  <bpmn:signal id="sig1"/>
  <bpmn:process id="p1">
    <bpmn:startEvent id="start"/>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="end"/>
  </bpmn:process>
  <bpmn:collaboration id="collab1">
    <bpmn:participant id="part1" processRef="p1"/>
    <bpmn:messageFlow id="mf1" sourceRef="part1" targetRef="part1" />
  </bpmn:collaboration>
</bpmn:definitions>
""";

    [Fact]
    public void CollaborationParsing_Disabled_ByDefault()
    {
        var model = new BpmnParser(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict })
            .ParseAsync(XmlWithCollab).GetAwaiter().GetResult();

        Assert.Equal("p1", model.ProcessId);
        Assert.Empty(model.Participants);          // still disabled (zero-break)
        Assert.Empty(model.MessageFlows);
        Assert.NotNull(model.RawMetadata);         // strict
        Assert.NotNull(model.RawMetadata!.RawGlobalElements); // existing capture
        // GlobalElementKinds index not built when option disabled
        Assert.Null(model.RawMetadata!.GlobalElementKinds);
    }

    [Fact]
    public void CollaborationParsing_Enabled_Participants_And_MessageFlows_Available()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            EnableCollaborationParsing = true,
            BuildGlobalElementIndex = true
        }).ParseAsync(XmlWithCollab).GetAwaiter().GetResult();

        Assert.Single(model.Participants);
        Assert.Equal("part1", model.Participants[0].Id);
        Assert.Single(model.MessageFlows);
        Assert.Equal("mf1", model.MessageFlows[0].Id);

        // Global element index present & correct
        Assert.NotNull(model.RawMetadata);
        Assert.NotNull(model.RawMetadata!.GlobalElementKinds);
        Assert.Equal("message", model.RawMetadata!.GlobalElementKinds!["msg1"]);
        Assert.Equal("signal", model.RawMetadata!.GlobalElementKinds!["sig1"]);
    }
}