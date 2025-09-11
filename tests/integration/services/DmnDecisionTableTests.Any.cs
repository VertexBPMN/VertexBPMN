using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {

        [Fact]
        public void Evaluate_AnyHitPolicy_ReturnsFirstMatchingResult()
        {
            // Arrange: DMN Decision Table with ANY hit policy
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "1" }, new List<string> { "A" }),
                    new DmnRule(new List<string> { "2" }, new List<string> { "B" }),
                    new DmnRule(new List<string> { "1" }, new List<string> { "A" }) // duplicate output
                },
                hitPolicy: "ANY"
            );
            var input = new Dictionary<string, object> { { "i1", 1 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: ANY returns the first matching output, and all outputs must be identical
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("A", result["output1"]);
        }

        [Fact]
        public void Evaluate_AnyHitPolicy_ThrowsIfOutputsDiffer()
        {
            // Arrange: DMN Decision Table with ANY hit policy, but different outputs for same input
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "1" }, new List<string> { "A" }),
                    new DmnRule(new List<string> { "1" }, new List<string> { "B" }) // different output
                },
                hitPolicy: "ANY"
            );
            var input = new Dictionary<string, object> { { "i1", 1 } };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => table.Evaluate(input));
        }
    }
}
