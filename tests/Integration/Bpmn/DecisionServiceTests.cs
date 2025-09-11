using VertexBPMN.EngineServices;

namespace VertexBPMN.Tests.Integration.Bpmn;

public class DecisionServiceTests
{
    [Fact]
    public async Task Evaluate_Decision_Returns_Inputs_As_Outputs()
    {
        var service = new DecisionService();
        var inputs = new Dictionary<string, object> { { "foo", 42 } };
        var result = await service.EvaluateDecisionByKeyAsync("test", inputs);
        Assert.NotNull(result);
        Assert.True(result.Outputs.ContainsKey("foo"));
        Assert.Equal(42, (result.Outputs["foo"] as int?) ?? ((System.Text.Json.JsonElement)result.Outputs["foo"]).GetInt32());
    }
}