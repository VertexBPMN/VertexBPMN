

using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Tests.Integration.Services;

public partial class DmnDecisionTableTests
{

	[Fact]
	public void Evaluate_AnyHitPolicy_ReturnsFirstMatchingResult()
	{
		// Arrange: DMN Decision Table with ANY hit policy
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["1"], ["A"]),
					new(["2"], ["B"]),
					new(["1"], ["A"]) // duplicate output
			],
			hitPolicy: "ANY"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: ANY returns the first matching output, and all outputs must be identical
		Assert.NotNull(result);
		Assert.Single(result);
		Assert.Equal("A", result["output1"]);
	}

	[Fact]
	public void Evaluate_AnyHitPolicy_ThrowsIfOutputsDiffer()
	{
		// Arrange: DMN Decision Table with ANY hit policy, but different outputs for same input
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["1"], ["A"]),
					new(["1"], ["B"]) // different output
			],
			hitPolicy: "ANY"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => table.Evaluate(input));
	}
}
