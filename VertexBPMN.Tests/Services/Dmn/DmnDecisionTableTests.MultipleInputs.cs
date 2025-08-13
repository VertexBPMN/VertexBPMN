using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_MultipleInputs_AllMustMatch()
        {
            // Arrange: Table with two inputs, both must match
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1"), new DmnInput("i2", "input2") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "42", "foo" }, new List<string> { "A" }),
                    new DmnRule(new List<string> { "42", "bar" }, new List<string> { "B" })
                },
                hitPolicy: "UNIQUE"
            );
            var input = new Dictionary<string, object> { { "i1", 42 }, { "i2", "foo" } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Only the rule where both inputs match should be selected
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("A", result["output1"]);
        }
    }
}
