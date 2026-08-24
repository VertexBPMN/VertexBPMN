using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_9_2_Test
    {
        [Fact]
        public void Test_C_9_2_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.9.2.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.9.2.bpmn");

            // Ergänzt: "Accelerate decision making" – Referenzmodell enthält 3 SubProcesse,
            // 1 CallActivity ("Decide on application" – verweist vermutlich auf C.9.0),
            // ExclusiveGateway ("Fraud detected?"), UserTask ("Decide Manually"),
            // BoundaryEvent (Timer "Timeout (7 days)"), SendTask, Message-/Error-/Timer-Events.
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            Assert.Contains(result, r => r.ToString().Contains("UserTask"));
            Assert.Contains(result, r => r.ToString().Contains("ExclusiveGateway"));
            // ACHTUNG: folgende Bezeichner bisher unbestätigtes Vokabular – ggf. anpassen.
            Assert.Contains(result, r => r.ToString().Contains("SubProcess"));
            Assert.Contains(result, r => r.ToString().Contains("CallActivity"));
            Assert.Contains(result, r => r.ToString().Contains("BoundaryEvent"));
        }
    }
}
