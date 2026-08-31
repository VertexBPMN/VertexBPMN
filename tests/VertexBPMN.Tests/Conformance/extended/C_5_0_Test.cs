using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_5_0_Test
    {
        [Fact]
        public async Task Test_C_5_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.5.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.5.0.bpmn");
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            // Ergänzt: EndEvent fehlte bisher, obwohl das Referenzmodell 4 endEvent-Elemente enthält.
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            Assert.Contains(result, r => r.ToString().Contains("UserTask"));
            Assert.Contains(result, r => r.ToString().Contains("ExclusiveGateway"));
            Assert.Contains(result, r => r.ToString().Contains("ParallelGateway"));
            Assert.Contains(result, r => r.ToString().Contains("ParallelBranch"));
        }
    }
}
