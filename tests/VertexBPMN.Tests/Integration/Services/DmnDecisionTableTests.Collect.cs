


using VertexBPMN.Domain.Model.Dmn;



namespace VertexBPMN.Tests.Integration.Services;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_CollectPolicy_ReturnsAllMatchingResultsAsList()
	{
		// Arrange: COLLECT policy, two rules match
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["1"], ["A"]),
					new(["1"], ["B"]),
					new(["2"], ["C"])
			],
			hitPolicy: "COLLECT"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should return all outputs for matching rules as a list
		Assert.NotNull(result);
		Assert.Single(result);
		var list = Assert.IsType<List<object>>(result["output1"]);
		Assert.Equal(2, list.Count);
		Assert.Contains("A", list);
		Assert.Contains("B", list);
		Assert.DoesNotContain("C", list);
	}
}
