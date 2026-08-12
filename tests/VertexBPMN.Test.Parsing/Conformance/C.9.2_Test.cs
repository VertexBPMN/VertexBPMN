using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Conformance
{
    public class C_9_2_Test
    {
        [Fact]
        public void Test_C_9_2_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.9.2.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser();
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new FullConformanceProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.9.2.bpmn");
            // TODO: Add specific assertions for expected result
        }
    }
}
