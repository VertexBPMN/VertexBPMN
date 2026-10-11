


using VertexBPMN.Domain.Model.Dmn;


namespace VertexBPMN.Tests.Integration.Services;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_InputEqualsSyntax_MatchesCorrectly()
	{
		// Arrange: Table with FEEL '=' syntax
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["=42"], ["A"]),
					new(["=99"], ["B"])
			],
			hitPolicy: "UNIQUE"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 42 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should match the rule with '=42'
		Assert.NotNull(result);
		Assert.Single(result);
		Assert.Equal("A", result["output1"]);
	}
}
