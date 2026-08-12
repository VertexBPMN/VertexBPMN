using System.Xml.Linq;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Serialization;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

/// <summary>
/// Phase C remaining plan RED tests (A–E). These are intentionally failing until implementation.
/// </summary>
public class StrictPhaseCRemainingPlanTests
{
    private static BpmnParser Strict() => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true, ParseDiagramInterchange = true });

    // A. RawDiRoot unverändert ausgeben
    [Fact]
    public void Strict_Emits_RawDiRoot_Unchanged() // RED
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:bpmndi='http://www.omg.org/spec/BPMN/20100524/DI' xmlns:omgdc='http://www.omg.org/spec/DD/20100524/DC' xmlns:omgdi='http://www.omg.org/spec/DD/20100524/DI'>
  <bpmn:process id='p1'><bpmn:startEvent id='s1'/></bpmn:process>
  <bpmndi:BPMNDiagram id='D1'>
    <bpmndi:BPMNPlane bpmnElement='p1'>
      <bpmndi:BPMNShape id='shape_s1' bpmnElement='s1'>
        <omgdc:Bounds x='10' y='20' width='36' height='36'/>
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";
        var model = Strict().ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata?.RawDiRoot);
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // Expect original BPMNDiagram fragment (single-line serializer acceptable) – must contain original id and shape id and bounds coords
        Assert.Contains("<bpmndi:BPMNDiagram id=\"D1\"", outXml); // expected to fail (not yet emitted from RawDiRoot)
        Assert.Contains("shape_s1", outXml);
        Assert.Contains("x='10'".Replace('\'', '"'), outXml.Replace('\'', '"'));
    }

    // B. LaneSet Struktur unverändert (laneSet->lane->flowNodeRef Reihenfolge)
    [Fact]
    public void Strict_Emits_LaneSet_Structure_Unchanged() // RED
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:laneSet id='ls1'>
      <bpmn:lane id='laneA'>
        <bpmn:flowNodeRef>t2</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>t1</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:task id='t1'/>
    <bpmn:task id='t2'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = Strict().ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata?.RawLanes); // captured
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // Expect laneSet wrapper present (current impl likely flattens lane + laneSet separate additions -> may duplicate or reorder)
        Assert.Contains("<bpmn:laneSet id=\"ls1\"", outXml); // may pass
        // Order of flowNodeRef EXACT: t2 then t1 inside same lane block
        var laneIdx = outXml.IndexOf("<bpmn:lane id=\"laneA\"");
        Assert.True(laneIdx >= 0);
        var laneEnd = outXml.IndexOf("</bpmn:lane>", laneIdx, StringComparison.Ordinal);
        var laneSlice = outXml.Substring(laneIdx, laneEnd - laneIdx);
        var firstT2 = laneSlice.IndexOf("<bpmn:flowNodeRef>t2</bpmn:flowNodeRef>", StringComparison.Ordinal);
        var firstT1 = laneSlice.IndexOf("<bpmn:flowNodeRef>t1</bpmn:flowNodeRef>", StringComparison.Ordinal);
        Assert.True(firstT2 >= 0 && firstT1 > firstT2, "flowNodeRef order not preserved"); // RED until structural emit keeps order
    }

    // C. NamespaceContext Reihenfolge exakt beibehalten + kein zusätzlicher bpmn Prefix, wenn ursprünglich nur Default-NS
    [Fact]
    public void Strict_Preserves_NamespaceContext_Order_And_No_Extra_BpmnPrefix_When_DefaultOnly() // RED
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:zzz='http://ex.com/z' xmlns:aaa='http://ex.com/a'>
  <process id='p1'/>
</definitions>"; // note: no explicit bpmn prefix originally
        var model = Strict().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // Expect xmlns order: (default) then zzz then aaa – and NO xmlns:bpmn injected
        var defStart = outXml.IndexOf("<definitions ", StringComparison.Ordinal);
        Assert.True(defStart >= 0);
        var defClose = outXml.IndexOf('>', defStart);
        var header = outXml.Substring(defStart, defClose - defStart);
        var idxDefault = header.IndexOf("xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"", StringComparison.Ordinal);
        var idxZ = header.IndexOf("xmlns:zzz=\"http://ex.com/z\"", StringComparison.Ordinal);
        var idxA = header.IndexOf("xmlns:aaa=\"http://ex.com/a\"", StringComparison.Ordinal);
        Assert.True(idxDefault >= 0 && idxZ > idxDefault && idxA > idxZ, "Namespace order not preserved");
        Assert.DoesNotContain("xmlns:bpmn=", header); // expected to fail currently (serializer adds bpmn prefix)
    }

    // D. Fallback Diagnostics wenn Raw-Blöcke fehlen (extensions Beispiel)
    [Fact]
    public void Fallback_Diagnostics_When_RawExtensions_Missing() // RED
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:v='http://vendor/x'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1'>
      <bpmn:extensionElements><v:prop k='x' v='1'/></bpmn:extensionElements>
    </bpmn:userTask>
  </bpmn:process>
</bpmn:definitions>";
        var model = Strict().ParseAsync(xml).GetAwaiter().GetResult();
        // simulate raw loss (e.g. mutation) but keep strict requested
        var rm = model.RawMetadata! with { RawExtensionElements = null };
        model = model with { RawMetadata = rm };
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // Expect diagnostic added (not implemented yet -> RED)
        Assert.Contains(model.Diagnostics, d => d.StartsWith("RT-Fallback:extensions", StringComparison.Ordinal));
        // Output should still include task element (fallback worked)
        Assert.Contains("<bpmn:userTask id=\"t1\"", outXml);
    }

    // E. DeepClone Schutz: Mutation nach Parse sollte Ausgabe NICHT beeinflussen
    [Fact]
    public void Strict_RawExtensions_Immutable_Snapshot() // RED (current behavior mutable)
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:v='http://vendor/x'>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'>
      <bpmn:extensionElements><v:x foo='orig'/></bpmn:extensionElements>
    </bpmn:startEvent>
  </bpmn:process>
</bpmn:definitions>";
        var model = Strict().ParseAsync(xml).GetAwaiter().GetResult();
        var raw = model.RawMetadata!.RawExtensionElements!;
        var before = raw["s1"].ToString(SaveOptions.DisableFormatting);
        // mutate raw
        raw["s1"].Elements().First().SetAttributeValue("foo", "changed");
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        Assert.Contains("foo=\"orig\"", outXml);
        Assert.DoesNotContain("foo=\"changed\"", outXml);
    }
}
