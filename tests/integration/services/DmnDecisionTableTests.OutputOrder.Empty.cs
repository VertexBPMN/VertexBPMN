using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_OutputOrderHitPolicy_EmptyResultIfNoMatch()
        {
            // Arrange: OUTPUT ORDER, but no rule matches
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "2" }, new List<string> { "B" }),
                    new DmnRule(new List<string> { "3" }, new List<string> { "A" })
                },
                hitPolicy: "OUTPUT ORDER"
            );
            var input = new Dictionary<string, object> { { "i1", 1 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Should return empty result
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
