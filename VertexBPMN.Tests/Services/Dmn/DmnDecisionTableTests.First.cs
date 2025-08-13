using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_FirstPolicy_ReturnsFirstMatchingResult()
        {
            // Arrange: FIRST policy, two rules match, should return first
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "1" }, new List<string> { "A" }),
                    new DmnRule(new List<string> { "1" }, new List<string> { "B" })
                },
                hitPolicy: "FIRST"
            );
            var input = new Dictionary<string, object> { { "i1", 1 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Should return the output of the first matching rule
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("A", result["output1"]);
        }
    }
}
