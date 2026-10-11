


using VertexBPMN.Domain.Model.Dmn;


namespace VertexBPMN.Tests.Integration.Services;

public partial class DmnDecisionTableTests
{
	private static readonly string[] ExpectedOrderedOutputs = ["A", "B", "C"];
	[Fact]
	public void Evaluate_OutputOrderHitPolicy_ReturnsResultsSortedByOutput()
	{
		// Arrange: DMN Decision Table with OUTPUT ORDER hit policy
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
			hitPolicy: "OUTPUT ORDER"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should return all outputs sorted (A, B, C)
		Assert.NotNull(result);
		Assert.Single(result);
		var list = Assert.IsType<List<object>>(result["output1"]);
		Assert.Equal(ExpectedOrderedOutputs, list);
	}
}
