
using VertexBPMN.Domain.Model.Dmn;


namespace VertexBPMN.Tests.Integration.Dmn;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_InputAsString_MatchesNumberRule()
	{
		// Arrange: Table with numeric rule, input as string
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["42"], ["A"])
			],
			hitPolicy: "UNIQUE"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", "42" } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should match the rule even if input is string
		Assert.NotNull(result);
		Assert.Single(result);
		Assert.Equal("A", result["output1"]);
	}
}
