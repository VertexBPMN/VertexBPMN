using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Dmn;

public class DmnDecisionTableTests
{
    [Fact]
    public void Parses_And_Evaluates_Simple_DecisionTable()
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
        var table = DmnDecisionTable.Parse(dmn);
        var result = table.Evaluate(new Dictionary<string, object> { { "i1", "18" } });
        Assert.Equal("adult", result["result"]);
        var result2 = table.Evaluate(new Dictionary<string, object> { { "i1", "16" } });
        Assert.Equal("teen", result2["result"]);
    }
}
