using System.IO;
using Xunit;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Tests.TestSuite2025
{
    public class C_9_2_Test
    {
        [Fact]
        public void Test_C_9_2_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.9.2.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var parser = new BpmnParser();
            var model = parser.Parse(xml);
            Assert.NotNull(model);
            var engine = new TokenEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.9.2.bpmn");
            // TODO: Add specific assertions for expected result
        }
    }
}
