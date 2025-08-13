using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Dmn;

public class DmnAnyCollectPolicyTests
{
    [Fact]
    public void Any_HitPolicy_Returns_First_Match()
    {
        const string dmn = @"<definitions xmlns='http://www.omg.org/spec/DMN/20191111/MODEL/'>
  <decision id='d1' name='Test'>
    <decisionTable hitPolicy='ANY'>
      <input id='i1'><inputExpression>age</inputExpression></input>
      <output id='o1' name='result'/>
      <rule>
        <inputEntry>18</inputEntry>
        <outputEntry>adult</outputEntry>
      </rule>
      <rule>
        <inputEntry>18</inputEntry>
        <outputEntry>adult</outputEntry>
      </rule>
    </decisionTable>
  </decision>
</definitions>";
        var table = DmnDecisionTable.Parse(dmn);
        var result = table.Evaluate(new Dictionary<string, object> { { "i1", "18" } });
        Assert.Equal("adult", result["result"]);
    }

    [Fact]
    public void Collect_HitPolicy_Returns_All_Matches()
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
        <outputEntry>duplicate</outputEntry>
      </rule>
    </decisionTable>
  </decision>
</definitions>";
        var table = DmnDecisionTable.Parse(dmn);
        var result = table.Evaluate(new Dictionary<string, object> { { "i1", "18" } });
        Assert.True(result["result"] is List<object> list && list.Contains("adult") && list.Contains("duplicate"));
    }
}
