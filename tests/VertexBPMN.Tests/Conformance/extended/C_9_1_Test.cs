using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_9_1_Test
    {
        [Fact]
        public void Test_C_9_1_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.9.1.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.9.1.bpmn");

            // Ergänzt: "Document Request" – Referenzmodell prüft Timer-Boundary-Events
            // ("1 week" / "daily" Reminder), SendTask ("Send reminder email"),
            // ReceiveTask ("Wait for answer"), UserTask ("Call customer").
            // Kein ExclusiveGateway/BusinessRuleTask in diesem Modell enthalten.
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            Assert.Contains(result, r => r.ToString().Contains("UserTask"));
            // ACHTUNG: folgende Bezeichner bisher unbestätigtes Vokabular – ggf. anpassen.
            Assert.Contains(result, r => r.ToString().Contains("SendTask"));
            Assert.Contains(result, r => r.ToString().Contains("BoundaryEvent"));
        }
    }
}
