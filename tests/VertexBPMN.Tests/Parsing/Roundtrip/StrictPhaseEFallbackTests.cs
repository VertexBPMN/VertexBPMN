using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class StrictPhaseEFallbackTests
{
    private static BpmnParser P => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public async Task Strict_Fallback_When_RawExtensions_Removed()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:ven='http://vendor/x'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1'>
      <bpmn:extensionElements><ven:alpha foo='bar'/></bpmn:extensionElements>
    </bpmn:userTask>
  </bpmn:process>
</bpmn:definitions>";
        var model = await P.ParseAsync(xml, TestContext.Current.CancellationToken);
        Assert.NotNull(model.RawMetadata);
        Assert.NotNull(model.RawMetadata!.RawExtensionElements);
        // simulate loss
        var rm = model.RawMetadata with { RawExtensionElements = null };
        model = model with { RawMetadata = rm };
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // Diagnostic present
        Assert.Contains(model.Diagnostics, d => d.StartsWith("RT-Fallback:extensions"));
        // Output still has task
        Assert.Contains("<bpmn:userTask id=\"t1\"", outXml);
        // Original vendor alpha element not necessarily preserved (fallback path)
        Assert.DoesNotContain("ven:alpha", outXml);
    }
}
