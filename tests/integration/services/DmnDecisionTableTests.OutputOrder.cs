using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_OutputOrderHitPolicy_ReturnsResultsSortedByOutput()
        {
            // Arrange: DMN Decision Table with OUTPUT ORDER hit policy
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "1" }, new List<string> { "B" }),
                    new DmnRule(new List<string> { "1" }, new List<string> { "A" }),
                    new DmnRule(new List<string> { "1" }, new List<string> { "C" })
                },
                hitPolicy: "OUTPUT ORDER"
            );
            var input = new Dictionary<string, object> { { "i1", 1 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Should return all outputs sorted (A, B, C)
            Assert.NotNull(result);
            Assert.Single(result);
            var list = Assert.IsType<List<object>>(result["output1"]);
            Assert.Equal(new[] { "A", "B", "C" }, list);
        }
    }
}
