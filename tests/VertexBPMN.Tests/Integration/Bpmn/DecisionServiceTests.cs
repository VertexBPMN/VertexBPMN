
using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Infrastructure.Persistence.InMemory;

namespace VertexBPMN.Tests.Integration.Bpmn;

public class DecisionServiceTests
{
    [Fact]
    public async Task Evaluate_Decision_Returns_Dmn_Output()
    {
        var logger = new LoggerFactory().CreateLogger<DecisionService>();
        var repository = new InMemoryDecisionRepository();
        var service = new DecisionService(logger, repository);
        const string dmnXml = """
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/">
              <decision id="test" name="Test">
                <decisionTable hitPolicy="UNIQUE">
                  <input id="foo"><inputExpression typeRef="number"><text>foo</text></inputExpression></input>
                  <output id="result" name="result" typeRef="string" />
                  <rule><inputEntry><text>&gt;= 40</text></inputEntry><outputEntry><text>"accepted"</text></outputEntry></rule>
                </decisionTable>
              </decision>
            </definitions>
            """;
        await service.DeployAsync("test", "Test", dmnXml);
        var inputs = new Dictionary<string, object> { { "foo", 42 } };

        var result = await service.EvaluateDecisionByKeyAsync("test", inputs);

        Assert.NotNull(result);
        Assert.Equal("accepted", result.Variables["result"]);
    }
}
