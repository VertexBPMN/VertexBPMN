using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance
{
    public class A_3_0_Test
    {
        [Fact]
        public async Task Test_A_3_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "A.3.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for A.3.0.bpmn");
            // TODO: Add specific assertions for expected result
        }
    }
}
