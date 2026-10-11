using VertexBPMN.Application;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class DmnQuotedReferenceTests
{
	[Fact]
	public void DecisionReferencesInsideEscapedStringQuotesAreNotRewritten()
	{
		const string xml = """
            <definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/" namespace="urn:quoted-reference">
              <decision id="base" name="base">
                <variable name="base" />
                <literalExpression><text>1</text></literalExpression>
              </decision>
              <decision id="result" name="result">
                <variable name="result" />
                <informationRequirement><requiredDecision href="#base" /></informationRequirement>
                <literalExpression><text>"escaped \"base\" value"</text></literalExpression>
              </decision>
            </definitions>
            """;

		var result = DmnDecisionGraph.Parse(xml, "result").Evaluate(new Dictionary<string, object>(StringComparer.Ordinal));

		Assert.Equal("escaped \"base\" value", result["result"]);
	}
}
