using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_InputEqualsSyntax_MatchesCorrectly()
        {
            // Arrange: Table with FEEL '=' syntax
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "=42" }, new List<string> { "A" }),
                    new DmnRule(new List<string> { "=99" }, new List<string> { "B" })
                },
                hitPolicy: "UNIQUE"
            );
            var input = new Dictionary<string, object> { { "i1", 42 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Should match the rule with '=42'
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("A", result["output1"]);
        }
    }
}
