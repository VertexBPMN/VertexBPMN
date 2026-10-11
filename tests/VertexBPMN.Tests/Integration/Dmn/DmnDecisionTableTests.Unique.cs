
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Tests.Integration.Dmn;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_UniquePolicy_ThrowsIfMultipleMatches()
	{
		// Arrange: UNIQUE policy, but two rules match
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
			hitPolicy: "UNIQUE"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act & Assert: Should throw due to multiple matches
		Assert.Throws<InvalidOperationException>(() => table.Evaluate(input));
	}
}
