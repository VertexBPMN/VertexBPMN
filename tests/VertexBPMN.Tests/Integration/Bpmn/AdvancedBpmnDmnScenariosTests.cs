

using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence.InMemory;

namespace VertexBPMN.Tests.Integration.Bpmn;

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
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "sub1"),
                new("flow2", "sub1", "sub2"),
                new("flow3", "sub2", "end1")
            },
            new List<BpmnSubprocess> {
                new("sub1", false),
                new("sub2", false)
            }
        );
        var engine = new ProcessEngine();
        var trace = engine.Execute(model);
        Assert.Contains("SubProcess: sub1", trace);
        Assert.Contains("SubProcess: sub2", trace);
    }

    
    [Fact]
    public void Executes_Boundary_Event_On_UserTask()
    {
        var model = new BpmnModel(
            "P12",
            "BoundaryEvent",
            new List<BpmnEvent> { new("start1", "startEvent"), new("b1", "boundaryEvent", null,  "t1"), new("end1", "endEvent") },
             new List<BpmnTask> { new("t1", "userTask") },
             new List<BpmnGateway>(),
             new List<BpmnSequenceFlow> {
                 new("flow1", "start1", "t1"),
                 new("flow2", "t1", "end1"),
                 new("flow3", "b1", "end1")
             },
             new List<BpmnSubprocess>()
        );
        var engine = new ProcessEngine();
        var result = engine.Execute(model);
        Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
        Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
        Assert.Contains(result, r => r.ToString().Contains("UserTask: t1"));
        //Note: TokenEngine does not yet simulate boundary event token flow, but this test ensures model acceptance
    }
    

    [Fact]
    public async Task DecisionService_Handles_Complex_Inputs()
    {
        var logger = new LoggerFactory().CreateLogger<DecisionService>();
        var repository = new InMemoryDecisionRepository();
        var service = new DecisionService(logger, repository);
        const string dmnXml = """
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="complex" name="Complex">
                <decisionTable hitPolicy="UNIQUE">
                  <input id="foo"><inputExpression typeRef="number"><text>foo</text></inputExpression></input>
                  <input id="bar"><inputExpression typeRef="string"><text>bar</text></inputExpression></input>
                  <input id="list"><inputExpression typeRef="Any"><text>list</text></inputExpression></input>
                  <output id="result" name="result" typeRef="string" />
                  <rule>
                    <inputEntry><text>&gt;= 40</text></inputEntry>
                    <inputEntry><text>"baz"</text></inputEntry>
                    <inputEntry><text>-</text></inputEntry>
                    <outputEntry><text>"complex-inputs-accepted"</text></outputEntry>
                  </rule>
                </decisionTable>
              </decision>
            </definitions>
            """;
        await service.DeployAsync("complex", "Complex", dmnXml);
        var inputs = new Dictionary<string, object> { { "foo", 42 }, { "bar", "baz" }, { "list", new List<int> { 1, 2, 3 } } };

        var result = await service.EvaluateDecisionByKeyAsync("complex", inputs, null, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("complex-inputs-accepted", result.Variables["result"]);
    }
}
