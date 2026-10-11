



using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Tests.Integration.Services;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_PriorityHitPolicy_ReturnsHighestPriorityResult()
	{
		// Arrange: DMN Decision Table with PRIORITY hit policy
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs:
			[
				new("o1", "output1", "string", new object[] { "A", "B", "C" })
			],
			rules:
			[
				new(["1"], ["B"]),
					new(["1"], ["A"]),
					new(["1"], ["C"])
			],
			hitPolicy: "PRIORITY"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should return the output with the highest priority (A < B < C)
		Assert.NotNull(result);
		Assert.Single(result);
		Assert.Equal("A", result["output1"]);
	}
}
