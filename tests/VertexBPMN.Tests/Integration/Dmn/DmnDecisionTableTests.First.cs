


using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Tests.Integration.Dmn;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_FirstPolicy_ReturnsFirstMatchingResult()
	{
		// Arrange: FIRST policy, two rules match, should return first
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["1"], ["A"]),
					new(["1"], ["B"])
			],
			hitPolicy: "FIRST"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should return the output of the first matching rule
		Assert.NotNull(result);
		Assert.Single(result);
		Assert.Equal("A", result["output1"]);
	}
}
