using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Domain.Model;
using VertexBPMN.Domain.Model.Bpmn;
using Xunit;

namespace VertexBPMN.Test.Parsing.Conformance
{
    public class B_1_0_Test
    {
        [Fact]
        public void Test_B_1_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "B.1.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser();
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for B.1.0.bpmn");
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
        }
    }
}
