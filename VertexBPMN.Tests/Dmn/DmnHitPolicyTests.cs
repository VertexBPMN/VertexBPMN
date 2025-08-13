using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Dmn;

public class DmnHitPolicyTests
{
    [Fact]
    public void Unique_HitPolicy_Returns_First_Match()
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
    }

    [Fact]
    public void First_HitPolicy_Returns_First_Match()
    {
        const string dmn = @"<definitions xmlns='http://www.omg.org/spec/DMN/20191111/MODEL/'>
  <decision id='d1' name='Test'>
    <decisionTable hitPolicy='FIRST'>
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
        Assert.Equal("adult", result["result"]);
    }
}
