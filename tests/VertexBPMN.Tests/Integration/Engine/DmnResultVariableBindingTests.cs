using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Engine;

/// <summary>
/// Verifies the BusinessRuleTask DMN execution loop:
///  - the bound decision is resolved from zeebe:calledDecision.decisionId,
///  - the full decision output is bound under zeebe:calledDecision.resultVariable
///    (default "result") so ioMapping sources like `result.riskLevel` resolve the
///    REAL decision output instead of the null-seeded fallback.
/// </summary>
public class DmnResultVariableBindingTests
{
    [Fact]
    public async Task BusinessRuleTask_Binds_Decision_Output_Under_ResultVariable_For_IoMapping()
    {
        var parser = new Mock<IDmnParser>();
        parser.Setup(x => x.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DmnDecision("risk_check", "Risk Check", [], [], [], "UNIQUE"));

        var dmnEngine = new Mock<IDmnEngine>();
        dmnEngine.Setup(x => x.EvaluateDecisionAsync(
                It.IsAny<DmnDecision>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>
            {
                ["riskLevel"] = "red",
                ["risk"] = "high"
            });

        var engine = new ProcessEngine(
            NullLogger<ProcessEngine>.Instance,
            NullServiceTaskRegistry.Instance,
            bpmnParser: null,
            dmnParser: parser.Object,
            dmnEngine: dmnEngine.Object);

        var taskAttrs = new Dictionary<string, string>
        {
            ["zeebe:calledDecision.decisionId"] = "risk_check",
            ["zeebe:calledDecision.resultVariable"] = "result",
            // output mapping referencing the bound decision-output object
            ["zeebe:ioMapping.output.riskLevels"] = "= if result != null then result.riskLevel else [\"green\"]"
        };
        var model = new BpmnModel(
            "p-risk",
            "Risk",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask> { new("brt", "businessRuleTask", null, taskAttrs) },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "brt"),
                new("flow2", "brt", "end1")
            },
            new List<BpmnSubprocess>());

        await engine.RegisterDmnModelAsync("risk_check", "<definitions />");
        var trace = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);

        // Decision must be resolved from zeebe:calledDecision.decisionId and evaluated
        dmnEngine.Verify(x => x.EvaluateDecisionAsync(
            It.IsAny<DmnDecision>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Output mapping must resolve the REAL decision output (result.riskLevel = "red"),
        Assert.Contains(trace, l => l.Contains("ZeebeIOMappingFeel")
            && l.Contains("riskLevels='red'", System.StringComparison.Ordinal));
        // ... and NOT fall back to the ["green"] else-branch.
        Assert.DoesNotContain(trace, l => l.Contains("riskLevels='[green]'")
            || l.Contains("riskLevels='[\"green\"]'")
            || l.Contains("riskLevels='green'"));
    }
}
