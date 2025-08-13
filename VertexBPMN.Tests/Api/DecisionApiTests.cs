using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using VertexBPMN.Api;
using Xunit;

namespace VertexBPMN.Tests.Api;

public class DecisionApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DecisionApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
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
        var post = await _client.PostAsJsonAsync("/api/decision/deploy", deploy);
        post.EnsureSuccessStatusCode();

        var eval = new { DecisionKey = "d1", Inputs = new Dictionary<string, object> { { "i1", "18" } } };
        var evalPost = await _client.PostAsJsonAsync("/api/decision/evaluate", eval);
        evalPost.EnsureSuccessStatusCode();
  var result = await evalPost.Content.ReadFromJsonAsync<DecisionResult>();
  Assert.NotNull(result);
  var output = result.Outputs["result"];
  string value = output is System.Text.Json.JsonElement je ? je.GetString() : output?.ToString();
  Assert.Equal("adult", value);
    }

    public record DecisionResult(Dictionary<string, object> Outputs);
}
