using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_CollectPolicy_ReturnsAllMatchingResultsAsList()
        {
            // Arrange: COLLECT policy, two rules match
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "1" }, new List<string> { "A" }),
                    new DmnRule(new List<string> { "1" }, new List<string> { "B" }),
                    new DmnRule(new List<string> { "2" }, new List<string> { "C" })
                },
                hitPolicy: "COLLECT"
            );
            var input = new Dictionary<string, object> { { "i1", 1 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Should return all outputs for matching rules as a list
            Assert.NotNull(result);
            Assert.Single(result);
            var list = Assert.IsType<List<object>>(result["output1"]);
            Assert.Equal(2, list.Count);
            Assert.Contains("A", list);
            Assert.Contains("B", list);
            Assert.DoesNotContain("C", list);
        }
    }
}
