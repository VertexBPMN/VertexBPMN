using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_MultipleOutputs_AllOutputsReturned()
        {
            // Arrange: Table with two outputs
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1"), new DmnOutput("o2", "output2") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "42" }, new List<string> { "A", "X" })
                },
                hitPolicy: "UNIQUE"
            );
            var input = new Dictionary<string, object> { { "i1", 42 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Both outputs should be present
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("A", result["output1"]);
            Assert.Equal("X", result["output2"]);
        }
    }
}
