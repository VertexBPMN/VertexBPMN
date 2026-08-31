using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class B_2_0_Test
    {
        [Fact]
        public async Task Test_B_2_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "B.2.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for B.2.0.bpmn");

            // B.2.0 validiert die deutlich umfangreichere "Analytic"-Konformitätsklasse.
            // Referenzmodell enthält u. a.: sendTask, receiveTask, serviceTask, boundaryEvent,
            // inclusiveGateway, eventBasedGateway, parallelGateway, subProcess, callActivity.
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            Assert.Contains(result, r => r.ToString().Contains("UserTask"));
            var foreignStartEvents = model.Events.Where(evt =>
                evt.Type == "startEvent" && evt.ProcessId != model.ProcessId).ToArray();
            Assert.NotEmpty(foreignStartEvents);
            Assert.DoesNotContain(foreignStartEvents, evt =>
                result.Any(entry => entry.Contains($"StartEvent: {evt.Id}", StringComparison.Ordinal)));
        }
    }
}
