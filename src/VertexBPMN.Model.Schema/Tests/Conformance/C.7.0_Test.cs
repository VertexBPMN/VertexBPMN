using Microsoft.Extensions.Logging;
using Moq;
using VertexBPMN.Domain.Model;
using VertexBPMN.Domain.Model.Bpmn;
using Xunit;

namespace VertexBPMN.Test.Parsing.Conformance
{
    public class C_7_0_Test
    {
        [Fact]
        public void Test_C_7_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.7.0.bpmn");
            var xml = File.ReadAllText(bpmnFile); // WICHTIG: kein Replace mehr!
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser();
            var model = parser.ParseAsync(xml).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.7.0.bpmn");
            Assert.Contains(result, r => r.Contains("StartEvent"));
            Assert.Contains(result, r => r.Contains("UserTask"));
        }
    }
}
