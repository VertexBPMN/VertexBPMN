using System.Net.Http.Json;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public class DecisionApiTests
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly CustomWebApplicationFactory _factory;

    public DecisionApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);
    }

    [Fact]
    public async Task Deploy_And_Evaluate_Dmn_Decision_Works()
    {
        const string dmn = @"<definitions xmlns='http://www.omg.org/spec/DMN/20191111/MODEL/'>
          <decision id='d1' name='Test'>
            <decisionTable hitPolicy='UNIQUE'>
              <input id='i1'><inputExpression>age</inputExpression></input>
              <output id='o1' name='result'/>
              <rule>
                <inputEntry>18</inputEntry>
                <outputEntry>adult</outputEntry>
              </rule>
              <rule>
                <inputEntry>16</inputEntry>
                <outputEntry>teen</outputEntry>
              </rule>
            </decisionTable>
          </decision>
        </definitions>";
        var deploy = new { DecisionKey = "d1", Name = "Test", DmnXml = dmn };
        var post = await _client.PostAsJsonAsync("/api/decision/deploy", deploy, cancellationToken: TestContext.Current.CancellationToken);
        post.EnsureSuccessStatusCode();

        var eval = new { DecisionKey = "d1", Inputs = new Dictionary<string, object> { { "i1", "18" } } };
        var evalPost = await _client.PostAsJsonAsync("/api/decision/evaluate", eval, cancellationToken: TestContext.Current.CancellationToken);
        evalPost.EnsureSuccessStatusCode();
        var result = await evalPost.Content.ReadFromJsonAsync<DecisionResult>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        var output = result.Variables["result"];
        string? value = output is System.Text.Json.JsonElement je ? je.GetString() : output?.ToString();
        Assert.Equal("adult", value);
    }

}
