using VertexBPMN.Core.Engine;
using VertexBPMN.Domain.Modeling;
using VertexBPMN.EngineServices;

namespace VertexBPMN.Tests.Integration.Engine;

public class TokenEngineTests
{
    [Fact]
    public void Executes_BusinessRuleTask_With_DecisionService()
    {
        var model = new BpmnModel(
            "P10",
            "Test",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask> { new("brt1", "businessRuleTask") },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "brt1"),
                new("flow2", "brt1", "end1")
            },
            new List<BpmnSubprocess>()
        );
        var engine = new TokenEngine();
        var decisionService = new DecisionService();
        var trace = engine.Execute(model, decisionService);
        Assert.Contains("BusinessRuleTask: brt1", trace);
        Assert.Contains("DecisionEvaluated: brt1 => 1", trace);
    }
    
    [Fact]
    public void Executes_ParallelGateway_Flow()
    {
        var model = new BpmnModel(
            "P4",
            "Test",
            new List<BpmnEvent> { new("start1", "startEvent") },
            new List<BpmnTask>(),
            new List<BpmnGateway> { new("gw1", "parallelGateway") },
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "gw1"),
                new("flow2", "gw1", "t1"),
                new("flow3", "gw1", "t2")
            },
            new List<BpmnSubprocess>()
        );
        var engine = new TokenEngine();
        var trace = engine.Execute(model);
        Assert.Contains("ParallelGateway: gw1", trace);
        Assert.Contains("ParallelBranch: t1", trace);
        Assert.Contains("ParallelBranch: t2", trace);
    }

    [Fact]
    public void Executes_InclusiveGateway_Flow()
    {
        var model = new BpmnModel(
            "P5",
            "Test",
            new List<BpmnEvent> { new("start1", "startEvent") },
            new List<BpmnTask>(),
            new List<BpmnGateway> { new("gw1", "inclusiveGateway") },
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "gw1"),
                new("flow2", "gw1", "t1"),
                new("flow3", "gw1", "t2")
            },
            new List<BpmnSubprocess>()
        );
        var engine = new TokenEngine();
        var trace = engine.Execute(model);
        Assert.Contains("InclusiveGateway: gw1", trace);
        Assert.Contains("InclusiveBranch: t1", trace);
        Assert.Contains("InclusiveBranch: t2", trace);
    }

    [Fact]
    public void Executes_Subprocess_And_MultiInstance()
    {
        var model = new BpmnModel(
            "P6",
            "Test",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "sub1"),
                new("flow2", "sub1", "end1")
            },
            new List<BpmnSubprocess> { new("sub1", true) }
        );
        var engine = new TokenEngine();
        var trace = engine.Execute(model);
        Assert.Contains("Subprocess: sub1", trace);
        Assert.Contains("MultiInstance: sub1", trace);
        Assert.Contains("SubprocessStart: sub1_start", trace);
        Assert.Contains("SubprocessEnd: sub1_end", trace);
    }

}