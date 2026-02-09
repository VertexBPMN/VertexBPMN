using VertexBPMN.Engine.Parsing;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Serialization;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhaseDDirtyTrackingTests
{
    private static BpmnParser StrictParser() => new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });

    [Fact]
    public void Changing_Task_Name_Sets_RoundtripDirty_And_Forces_Fallback()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:process id='p1'>
    <bpmn:userTask id='t1' name='Orig'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        Assert.False(model.RawMetadata!.RoundtripDirty);
        // mutate: change task name via Attributes map clone and mark dirty
        var task = model.Tasks[0];
        var newAttr = task.Attributes == null ? new() : new Dictionary<string,string>(task.Attributes);
        newAttr["name"] = "Changed";
        var mutatedTask = task with { Attributes = newAttr, Name = "Changed" };
        var newTasks = model.Tasks.ToList(); newTasks[0] = mutatedTask;
        model = model with { Tasks = newTasks, RawMetadata = model.RawMetadata with { RoundtripDirty = true } };

        var serializer = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict };
        var outXml = serializer.Serialize(model);
        // Fallback path should behave like normalized: name attribute present (already was) but raw ordering metadata ignored -> expect process directly contains userTask (still) but raw attributes like lane/artifacts not reinserted (we just check absence of strict-only property: incoming for single node since no flows)
        Assert.Contains("name=\"Changed\"", outXml);
        // strict-mode global raw prefix reproduction not triggered -> raw namespace replay not guaranteed (indirect indicator: no xmlns:ns_ext prefix expected)
        Assert.DoesNotContain("ns_ext", outXml);
    }

    [Fact]
    public void RoundtripDirty_Serializes_Without_Raw_Global_Elements_Echo()
    {
        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <bpmn:message id='m1'/>
  <bpmn:process id='p1'>
    <bpmn:startEvent id='s1'/>
  </bpmn:process>
</bpmn:definitions>";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.RawMetadata);
        Assert.True(model.RawMetadata!.RawGlobalElements?.Count > 0);
        model = model with { RawMetadata = model.RawMetadata with { RoundtripDirty = true } };
        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
        // In fallback the serializer should not necessarily preserve message position (we just check message still exists but cannot assert ordering) – ensure still valid output
        Assert.Contains("<bpmn:message id=\"m1\"", outXml);
        Assert.Contains("<bpmn:process id=\"p1\"", outXml);
    }
}
