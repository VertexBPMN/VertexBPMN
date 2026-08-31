using Microsoft.Extensions.Logging;
using Moq;

using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Engine;

public class DmnEngineTests
{
    private readonly Mock<ILogger<DmnEngine>> _loggerMock;
    private readonly DmnEngine _engine;

    public DmnEngineTests()
    {
        _loggerMock = new Mock<ILogger<DmnEngine>>();
        _engine = new DmnEngine(_loggerMock.Object);
    }

    [Fact]
    public async Task EvaluateDecisionAsync_FirstHitPolicy_ReturnsFirstMatchingRule()
    {
        var decision = new DmnDecision(
            Id: "creditDecision",
            Name: "Credit Approval",
            Inputs: new List<DmnInput> { new DmnInput("input1", "Age", "integer"), new DmnInput("input2", "Income", "integer") },
            Outputs: new List<DmnOutput> { new DmnOutput("output1", "Approval", "string") },
            Rules: new List<DmnRule>
            {
                new DmnRule("rule1", new Dictionary<string, string> { { "input1", ">=18" }, { "input2", ">30000" } }, new Dictionary<string, object> { { "output1", "Approved" } }),
                new DmnRule("rule2", new Dictionary<string, string> { { "input1", ">=18" }, { "input2", "<=30000" } }, new Dictionary<string, object> { { "output1", "Denied" } })
            },
            HitPolicy: "FIRST"
        );

        var variables = new Dictionary<string, object> { { "Age", 25 }, { "Income", 40000 } };
        var result = await _engine.EvaluateDecisionAsync(decision, variables, TestContext.Current.CancellationToken);

        Assert.Equal("Approved", result["output1"]);
    }

    [Fact]
    public async Task EvaluateDecisionAsync_CollectHitPolicy_ReturnsAllMatchingOutputs()
    {
        var decision = new DmnDecision(
            Id: "discountDecision",
            Name: "Discount Calculation",
            Inputs: new List<DmnInput> { new DmnInput("input1", "OrderValue", "integer") },
            Outputs: new List<DmnOutput> { new DmnOutput("output1", "Discount", "integer") },
            Rules: new List<DmnRule>
            {
                new DmnRule("rule1", new Dictionary<string, string> { { "input1", ">100" } }, new Dictionary<string, object> { { "output1", 10 } }),
                new DmnRule("rule2", new Dictionary<string, string> { { "input1", ">200" } }, new Dictionary<string, object> { { "output1", 20 } })
            },
            HitPolicy: "COLLECT"
        );

        var variables = new Dictionary<string, object> { { "OrderValue", 250 } };
        var result = await _engine.EvaluateDecisionAsync(decision, variables, TestContext.Current.CancellationToken);

        var discounts = (List<object>)result["output1"];
        Assert.Equal(2, discounts.Count);
        Assert.Contains(10, discounts);
        Assert.Contains(20, discounts);
    }

    [Fact]
    public async Task EvaluateDecisionAsync_NoMatchingRules_ThrowsException()
    {
        var decision = new DmnDecision(
            Id: "creditDecision",
            Name: "Credit Approval",
            Inputs: new List<DmnInput> { new DmnInput("input1", "Age", "integer") },
            Outputs: new List<DmnOutput> { new DmnOutput("output1", "Approval", "string") },
            Rules: new List<DmnRule> { new DmnRule("rule1", new Dictionary<string, string> { { "input1", ">=18" } }, new Dictionary<string, object> { { "output1", "Approved" } }) },
            HitPolicy: "UNIQUE"
        );

        var variables = new Dictionary<string, object> { { "Age", 15 } };
        await Assert.ThrowsAsync<DmnEvaluationException>(() => _engine.EvaluateDecisionAsync(decision, variables, TestContext.Current.CancellationToken));
    }
}