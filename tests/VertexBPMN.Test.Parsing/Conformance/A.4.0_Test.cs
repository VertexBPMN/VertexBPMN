using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Engine;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Conformance
{
    public class A_4_0_Test
    {
        [Fact]
        public void Test_A_4_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "A.4.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser();
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new FullConformanceProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for A.4.0.bpmn");
            // TODO: Add specific assertions for expected result
        }
    }
}
