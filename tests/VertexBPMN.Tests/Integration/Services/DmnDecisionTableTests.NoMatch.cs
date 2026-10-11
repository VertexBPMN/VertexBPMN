



using VertexBPMN.Domain.Model.Dmn;


namespace VertexBPMN.Tests.Integration.Services;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_InputDoesNotMatchAnyRule_ReturnsEmptyResult()
	{
		// Arrange: Table with rules, but input does not match any rule
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["2"], ["A"]),
					new(["3"], ["B"])
			],
			hitPolicy: "UNIQUE"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 1 } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Should return empty result
		Assert.NotNull(result);
		Assert.Empty(result);
	}
}
