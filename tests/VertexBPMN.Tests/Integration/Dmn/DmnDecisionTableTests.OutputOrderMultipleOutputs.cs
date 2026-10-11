
using VertexBPMN.Domain.Model.Dmn;


namespace VertexBPMN.Tests.Integration.Dmn;

public partial class DmnDecisionTableTests
{
	private static readonly string[] OutputOrderExpectedFirstOutput = ["A", "B"];
	private static readonly string[] OutputOrderExpectedSecondOutput = ["X", "Y"];
	[Fact]
	public void Evaluate_OutputOrderPolicy_MultipleOutputs_ReturnsSortedLists()
	{
		// Arrange: OUTPUT ORDER policy, two outputs, two rules match
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs:
			[
				new("o1", "output1", "string", new object[] { "A", "B" }),
					new("o2", "output2")
			],
			rules:
			[
				new(["42"], ["B", "Y"]),
					new(["42"], ["A", "X"])
			],
			hitPolicy: "OUTPUT ORDER"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 42 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Each output should be a sorted list of values
		Assert.NotNull(result);
		Assert.Equal(2, result.Count);
		var list1 = Assert.IsType<List<object>>(result["output1"]);
		var list2 = Assert.IsType<List<object>>(result["output2"]);
		Assert.Equal(OutputOrderExpectedFirstOutput, list1);
		Assert.Equal(OutputOrderExpectedSecondOutput, list2);
	}
}
