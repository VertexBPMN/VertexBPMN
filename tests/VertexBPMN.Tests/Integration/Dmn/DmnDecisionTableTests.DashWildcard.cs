


using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Tests.Integration.Dmn;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_InputWithDash_MatchesAnyValue()
	{
		// Arrange: Table with dash as input entry (wildcard)
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["-"], ["A"])
			],
			hitPolicy: "UNIQUE"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 42 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should match any value and return the output
		Assert.NotNull(result);
		Assert.Single(result);
		Assert.Equal("A", result["output1"]);
	}
}
