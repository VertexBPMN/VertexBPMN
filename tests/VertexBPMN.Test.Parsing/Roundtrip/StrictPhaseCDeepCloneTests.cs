using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhaseCDeepCloneTests
{
    private static BpmnParser P => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void RawExtensionElements_Mutation_Reflected_In_Output()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:v='http://vendor/x'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'>
      <bpmn:extensionElements>
        <v:alpha foo='bar'>
          <v:beta val='1'/>
        </v:alpha>
      </bpmn:extensionElements>
    </bpmn:startEvent>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        var rawExt = model.RawMetadata!.RawExtensionElements!;
        Assert.True(rawExt.ContainsKey("s1"));
        rawExt["s1"].Elements().First().SetAttributeValue("foo", "mutated");
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("foo=\"mutated\"", outXml);
        Assert.DoesNotContain("foo=\"bar\"", outXml);
    }

//    [Fact]
//    public void IncomingOutgoing_Generated_When_PreserveGeneratedIfMissing_True()
//    {
//        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
//  <bpmn:process id='p1'>
//    <bpmn:startEvent id='s1'/>
//    <bpmn:task id='t1'/>
//    <bpmn:sequenceFlow id='f1' sourceRef='s1' targetRef='t1'/>
//  </bpmn:process>
//</bpmn:definitions>";
//        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
//        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict, PreserveGeneratedIfMissing = true }.Serialize(model);
//        var doc = XDocument.Parse(outXml);
//        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
//        var s1 = doc.Descendants(bpmn + "startEvent").First(e => (string)e.Attribute("id") == "s1");
//        var t1 = doc.Descendants(bpmn + "task").First(e => (string)e.Attribute("id") == "t1");
//        var s1Outgoing = s1.Elements(bpmn + "outgoing").Select(e => e.Value).ToList();
//        var t1Incoming = t1.Elements(bpmn + "incoming").Select(e => e.Value).ToList();
//        Assert.Single(s1Outgoing);
//        Assert.Single(t1Incoming);
//        Assert.Equal("f1", s1Outgoing[0]);
//        Assert.Equal("f1", t1Incoming[0]);
//    }

//    [Fact]
//    public void IncomingOutgoing_Not_Generated_When_PreserveGeneratedIfMissing_False()
//    {
//        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
//  <bpmn:process id='p1'>
//    <bpmn:startEvent id='s1'/>
//    <bpmn:task id='t1'/>
//    <bpmn:sequenceFlow id='f1' sourceRef='s1' targetRef='t1'/>
//  </bpmn:process>
//</bpmn:definitions>";
//        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
//        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict, PreserveGeneratedIfMissing = false }.Serialize(model);
//        var doc = XDocument.Parse(outXml);
//        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
//        var s1 = doc.Descendants(bpmn + "startEvent").First(e => (string)e.Attribute("id") == "s1");
//        var t1 = doc.Descendants(bpmn + "task").First(e => (string)e.Attribute("id") == "t1");
//        Assert.Empty(s1.Elements(bpmn + "outgoing"));
//        Assert.Empty(t1.Elements(bpmn + "incoming"));
//    }

    [Fact]
    public void RawMultiInstance_Node_Not_Altered_By_Serializer()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1'>
      <bpmn:multiInstanceLoopCharacteristics isSequential='true'>
        <bpmn:loopCardinality>3</bpmn:loopCardinality>
      </bpmn:multiInstanceLoopCharacteristics>
    </bpmn:userTask>
  </bpmn:process>
</bpmn:definitions>";
        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        var doc = XDocument.Parse(outXml);
        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        var mi = doc.Descendants(bpmn + "multiInstanceLoopCharacteristics").First();
        Assert.Equal("true", (string?)mi.Attribute("isSequential"));
        Assert.Single(mi.Elements(bpmn + "loopCardinality"));
        Assert.Equal("3", mi.Element(bpmn + "loopCardinality")!.Value);
    }
}
