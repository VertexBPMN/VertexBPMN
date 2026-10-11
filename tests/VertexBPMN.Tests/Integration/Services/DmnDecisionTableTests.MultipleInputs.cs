



using VertexBPMN.Domain.Model.Dmn;


namespace VertexBPMN.Tests.Integration.Services;

public partial class DmnDecisionTableTests
{
	[Fact]
	public void Evaluate_MultipleInputs_AllMustMatch()
	{
		// Arrange: Table with two inputs, both must match
		var table = new DmnDecisionTable(
			key: "d1",
			name: "Test",
			inputs: [new("i1", "input1"), new("i2", "input2")],
			outputs: [new("o1", "output1")],
			rules:
			[
				new(["42", "foo"], ["A"]),
					new(["42", "bar"], ["B"])
			],
			hitPolicy: "UNIQUE"
		);
		var input = new Dictionary<string, object>(StringComparer.Ordinal) { { "i1", 42 }, { "i2", "foo" } };

		// Act
		var result = table.Evaluate(input);

		// Assert: Only the rule where both inputs match should be selected
		Assert.NotNull(result);
		Assert.Single(result);
		Assert.Equal("A", result["output1"]);
	}
}
