using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_6_0_Test
    {
        [Fact]
        public void Test_C_6_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.6.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.6.0.bpmn");

            // Ergänzt: bisher gab es hier keinerlei inhaltliche Assertion, nur ein Console.WriteLine.
            // Referenzmodell ("Travel Booking") enthält u. a.: sendTask, serviceTask,
            // boundaryEvent, eventBasedGateway, intermediateCatchEvent/-ThrowEvent, subProcess (Event-Subprocess).
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            Assert.Contains(result, r => r.Contains("EventBasedGateway", StringComparison.Ordinal));
            var foreignStartEvents = model.Events.Where(evt =>
                evt.Type == "startEvent" && evt.ProcessId != model.ProcessId).ToArray();
            Assert.NotEmpty(foreignStartEvents);
            Assert.DoesNotContain(foreignStartEvents, evt =>
                result.Any(entry => entry.Contains($"StartEvent: {evt.Id}", StringComparison.Ordinal)));

            foreach (var item in result)
            {
                Console.WriteLine($"Result item: {item}");
            }
        }
    }
}
