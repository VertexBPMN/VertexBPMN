


using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Tests.Integration.Dmn;

public partial class DmnDecisionTableTests
{
	private static readonly string[] CollectExpectedFirstOutput = ["A", "B"];
	private static readonly string[] CollectExpectedSecondOutput = ["X", "Y"];
	[Fact]
	public void Evaluate_CollectPolicy_MultipleOutputs_ReturnsListsForEachOutput()
	{
		// Arrange: COLLECT policy, two outputs, two rules match
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1"), new("o2", "output2")],
			rules:
			[
				new(["42"], ["A", "X"]),
					new(["42"], ["B", "Y"])
			],
			hitPolicy: "COLLECT"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 42 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Each output should be a list of values
		Assert.NotNull(result);
		Assert.Equal(2, result.Count);
		var list1 = Assert.IsType<List<object>>(result["output1"]);
		var list2 = Assert.IsType<List<object>>(result["output2"]);
		Assert.Equal(CollectExpectedFirstOutput, list1);
		Assert.Equal(CollectExpectedSecondOutput, list2);
	}
}
