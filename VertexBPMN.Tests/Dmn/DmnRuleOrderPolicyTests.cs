using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Dmn;

public class DmnRuleOrderPolicyTests
{
    [Fact]
    public void RuleOrder_HitPolicy_Returns_All_Matches_In_Order()
    {
        const string dmn = @"<definitions xmlns='http://www.omg.org/spec/DMN/20191111/MODEL/'>
  <decision id='d1' name='Test'>
    <decisionTable hitPolicy='RULE ORDER'>
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
        Assert.True(result["result"] is List<object> list && list[0].Equals("adult") && list[1].Equals("duplicate"));
    }
}
