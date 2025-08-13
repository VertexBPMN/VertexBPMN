using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_InputWithDash_MatchesAnyValue()
        {
            // Arrange: Table with dash as input entry (wildcard)
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "-" }, new List<string> { "A" })
                },
                hitPolicy: "UNIQUE"
            );
            var input = new Dictionary<string, object> { { "i1", 42 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Should match any value and return the output
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("A", result["output1"]);
        }
    }
}
