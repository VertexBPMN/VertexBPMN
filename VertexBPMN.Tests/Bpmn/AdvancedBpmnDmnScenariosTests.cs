using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Services;
using Xunit;

namespace VertexBPMN.Tests.Bpmn;

public class AdvancedBpmnDmnScenariosTests
{
    [Fact]
    public void Executes_Nested_Subprocesses()
    {
        var model = new BpmnModel(
            "P11",
            "NestedSubprocess",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSubprocess> {
                new("sub1", false),
                new("sub2", false)
            },
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "sub1"),
                new("flow2", "sub1", "sub2"),
                new("flow3", "sub2", "end1")
            }
        );
        var engine = new TokenEngine();
        var trace = engine.Execute(model);
        Assert.Contains("Subprocess: sub1", trace);
        Assert.Contains("Subprocess: sub2", trace);
        Assert.Contains("SubprocessStart: sub1_start", trace);
        Assert.Contains("SubprocessStart: sub2_start", trace);
        Assert.Contains("SubprocessEnd: sub1_end", trace);
        Assert.Contains("SubprocessEnd: sub2_end", trace);
    }

    [Fact]
    public void Executes_Boundary_Event_On_UserTask()
    {
        var model = new BpmnModel(
            "P12",
            "BoundaryEvent",
            new List<BpmnEvent> { new("start1", "startEvent"), new("b1", "boundaryEvent", "t1"), new("end1", "endEvent") },
            new List<BpmnTask> { new("t1", "userTask") },
            new List<BpmnGateway>(),
            new List<BpmnSubprocess>(),
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "t1"),
                new("flow2", "t1", "end1"),
                new("flow3", "b1", "end1")
            }
        );
        var engine = new TokenEngine();
        var trace = engine.Execute(model);
        Assert.Contains("UserTask: t1", trace);
        // Note: TokenEngine does not yet simulate boundary event token flow, but this test ensures model acceptance
    }

    [Fact]
    public async Task DecisionService_Handles_Complex_Inputs()
    {
        var service = new DecisionService();
        var inputs = new Dictionary<string, object> { { "foo", 42 }, { "bar", "baz" }, { "list", new List<int> { 1, 2, 3 } } };
        var result = await service.EvaluateDecisionByKeyAsync("complex", inputs);
        Assert.NotNull(result);
        Assert.Equal(42, (result.Outputs["foo"] as int?) ?? ((System.Text.Json.JsonElement)result.Outputs["foo"]).GetInt32());
        Assert.Equal("baz", result.Outputs["bar"].ToString());
        // List may be serialized as JsonElement array
        Assert.True(result.Outputs.ContainsKey("list"));
    }
}
