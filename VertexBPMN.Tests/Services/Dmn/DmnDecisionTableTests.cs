using System.Collections.Generic;
using Xunit;
using VertexBPMN.Core.Services.Dmn;

namespace VertexBPMN.Tests.Services.Dmn;

public partial class DmnDecisionTableTests
{
    [Fact]
    public void Evaluate_CollectPolicy_ReturnsAllMatchingResults()
    {
        const string dmn = @"<definitions xmlns='http://www.omg.org/spec/DMN/20191111/MODEL/'>
  <decision id='d1' name='Test'>
    <decisionTable hitPolicy='COLLECT'>
      <input id='i1'><inputExpression>age</inputExpression></input>
      <output id='o1' name='result'/>
      <rule>
        <inputEntry>18</inputEntry>
        <outputEntry>adult</outputEntry>
      </rule>
      <rule>
        <inputEntry>18</inputEntry>
        <outputEntry>voter</outputEntry>
      </rule>
      <rule>
        <inputEntry>16</inputEntry>
        <outputEntry>teen</outputEntry>
      </rule>
    </decisionTable>
  </decision>
</definitions>";
        var table = DmnDecisionTable.Parse(dmn);
        var result = table.Evaluate(new Dictionary<string, object> { { "i1", "18" } });
        Assert.True(result.ContainsKey("result"));
        var list = Assert.IsType<List<object>>(result["result"]);
        Assert.Contains("adult", list);
        Assert.Contains("voter", list);
        Assert.DoesNotContain("teen", list);
    }
}
