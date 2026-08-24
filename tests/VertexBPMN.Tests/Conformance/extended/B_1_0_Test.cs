using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class B_1_0_Test
    {
        [Fact]
        public void Test_B_1_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "B.1.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for B.1.0.bpmn");

            // B.1.0 validiert, dass alle für die "Descriptive"-Konformitätsklasse
            // vorgeschriebenen Elemente vorhanden sind (nicht nur Start/End).
            // Referenzmodell enthält u. a.: userTask, serviceTask, callActivity,
            // subProcess, exclusiveGateway, parallelGateway.
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            Assert.Contains(result, r => r.ToString().Contains("UserTask"));
            Assert.Contains(result, r => r.ToString().Contains("ExclusiveGateway"));
            // ACHTUNG: folgende Bezeichner bisher unbestätigtes Vokabular – ggf. anpassen,
            // falls euer Trace andere Namen für ServiceTask/ParallelGateway/CallActivity/SubProcess nutzt.
            Assert.Contains(result, r => r.ToString().Contains("ServiceTask"));
            Assert.Contains(result, r => r.ToString().Contains("ParallelGateway"));
            Assert.Contains(result, r => r.ToString().Contains("CallActivity"));
            Assert.Contains(result, r => r.ToString().Contains("SubProcess"));
        }
    }
}
