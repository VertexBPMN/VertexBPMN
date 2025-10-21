
using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Infrastructure.Persistence.InMemory;

namespace VertexBPMN.Tests.Integration.Bpmn;

public class DecisionServiceTests
{
    [Fact]
    public async Task Evaluate_Decision_Returns_Inputs_As_Outputs()
    {
        var logger = new LoggerFactory().CreateLogger<DecisionService>();
        var service = new DecisionService(logger, new InMemoryDecisionRepository());
        var inputs = new Dictionary<string, object> { { "foo", 42 } };
        var result = await service.EvaluateDecisionByKeyAsync("test", inputs);
        Assert.NotNull(result);
        Assert.True(result.Variables.ContainsKey("foo"));
        Assert.Equal(42, (result.Variables["foo"] as int?) ?? ((System.Text.Json.JsonElement)result.Variables["foo"]).GetInt32());
    }
}