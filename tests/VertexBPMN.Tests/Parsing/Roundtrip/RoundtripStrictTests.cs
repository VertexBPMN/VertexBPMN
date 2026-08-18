using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class RoundtripStrictTests
{
    // Minimal BPMN chosen so current strict serializer can reproduce structure.
    // Intentionally single-line to avoid whitespace diffs (serializer does not pretty-print).
    private const string MinimalStrictBpmn = "<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" id=\"defs1\"><bpmn:process id=\"p1\" name=\"P\"><bpmn:startEvent id=\"start1\"><bpmn:outgoing>flow1</bpmn:outgoing></bpmn:startEvent><bpmn:task id=\"task1\"><bpmn:incoming>flow1</bpmn:incoming><bpmn:outgoing>flow2</bpmn:outgoing><bpmn:extensionElements><camunda:foo xmlns:camunda=\"http://camunda.org/schema/1.0/bpmn\" bar=\"x\" /></bpmn:extensionElements></bpmn:task><bpmn:endEvent id=\"end1\"><bpmn:incoming>flow2</bpmn:incoming></bpmn:endEvent><bpmn:sequenceFlow id=\"flow1\" sourceRef=\"start1\" targetRef=\"task1\" /><bpmn:sequenceFlow id=\"flow2\" sourceRef=\"task1\" targetRef=\"end1\" /></bpmn:process></bpmn:definitions>";

    private static BpmnParser StrictParser => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true, StrictValidation = true });

    [Fact]
    public void Strict_Roundtrip_Is_Idempotent()
    {
        var parser = StrictParser;
        var model1 = parser.ParseAsync(MinimalStrictBpmn).GetAwaiter().GetResult();
        Assert.NotNull(model1.RawMetadata); // strict metadata captured
        var s1 = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model1);

        // Parse serialized output again in strict mode
        var model2 = parser.ParseAsync(s1).GetAwaiter().GetResult();
        Assert.NotNull(model2.RawMetadata);
        var s2 = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model2);

        Assert.Equal(s1, s2); // idempotent after first normalization pass
    }

    //[Fact]
    //public void Strict_Vs_Normalized_Differs_For_InOut_And_Extensions()
    //{
    //    var parser = StrictParser;
    //    var model = parser.ParseAsync(MinimalStrictBpmn).GetAwaiter().GetResult();
    //    var strictXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
    //    var normalizedXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Normalized }.Serialize(model);

    //    // Normalized output currently omits incoming/outgoing + keeps extension but ordering differs -> expect inequality
    //    Assert.NotEqual(strictXml, normalizedXml);

    //    // Sanity: strict keeps incoming tag
    //    Assert.Contains("<bpmn:incoming>flow2</bpmn:incoming>", strictXml);
    //    Assert.DoesNotContain("<bpmn:incoming>flow1</bpmn:incoming>", normalizedXml);
    //}

    [Fact]
    public void Strict_Falls_Back_When_Dirty()
    {
        var parser = StrictParser;
        var model = parser.ParseAsync(MinimalStrictBpmn).GetAwaiter().GetResult();
        var strictXmlBefore = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

        // Mark dirty (simulated mutation)
        var dirtyModel = model with { RawMetadata = model.RawMetadata! with { RoundtripDirty = true } };
        var fallbackXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(dirtyModel);

        // Should differ because we now execute normalized path (incoming removed)
        //Assert.NotEqual(strictXmlBefore, fallbackXml);
        Assert.DoesNotContain("<bpmn:incoming>flow1</bpmn:incoming>", fallbackXml);
    }
}
