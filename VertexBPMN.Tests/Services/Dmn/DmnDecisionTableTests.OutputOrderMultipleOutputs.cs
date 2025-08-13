using System;
using System.Collections.Generic;
using VertexBPMN.Core.Services.Dmn;
using Xunit;

namespace VertexBPMN.Tests.Services.Dmn
{
    public partial class DmnDecisionTableTests
    {
        [Fact]
        public void Evaluate_OutputOrderPolicy_MultipleOutputs_ReturnsSortedLists()
        {
            // Arrange: OUTPUT ORDER policy, two outputs, two rules match
            var table = new DmnDecisionTable(
                key: "d1",
                name: "Test",
                inputs: new List<DmnInput> { new DmnInput("i1", "input1") },
                outputs: new List<DmnOutput> { new DmnOutput("o1", "output1"), new DmnOutput("o2", "output2") },
                rules: new List<DmnRule>
                {
                    new DmnRule(new List<string> { "42" }, new List<string> { "B", "Y" }),
                    new DmnRule(new List<string> { "42" }, new List<string> { "A", "X" })
                },
                hitPolicy: "OUTPUT ORDER"
            );
            var input = new Dictionary<string, object> { { "i1", 42 } };

            // Act
            var result = table.Evaluate(input);

            // Assert: Each output should be a sorted list of values
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            var list1 = Assert.IsType<List<object>>(result["output1"]);
            var list2 = Assert.IsType<List<object>>(result["output2"]);
            Assert.Equal(new[] { "A", "B" }, list1);
            Assert.Equal(new[] { "X", "Y" }, list2);
        }
    }
}
