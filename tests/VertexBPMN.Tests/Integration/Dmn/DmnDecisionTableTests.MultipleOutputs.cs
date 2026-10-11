


using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Tests.Integration.Dmn;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_MultipleOutputs_AllOutputsReturned()
	{
		// Arrange: Table with two outputs
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1"), new("o2", "output2")],
			rules:
			[
				new(["42"], ["A", "X"])
			],
			hitPolicy: "UNIQUE"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 42 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Both outputs should be present
		Assert.NotNull(result);
		Assert.Equal(2, result.Count);
		Assert.Equal("A", result["output1"]);
		Assert.Equal("X", result["output2"]);
	}
}
