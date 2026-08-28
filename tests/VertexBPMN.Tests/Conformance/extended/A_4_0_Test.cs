using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class A_4_0_Test
    {
        [Fact]
        public void Test_A_4_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "A.4.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = parser.ParseAsync(xml.Replace('\'', '"'), CancellationToken.None).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for A.4.0.bpmn");

            // A.4.0 prüft grafische Elemente in expandierten SubProcessen, Lanes und Pools.
            // Referenzmodell: 2 process/participant, 2 lane, 2 subProcess, 4 startEvent, 5 endEvent.
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            var foreignStartEvents = model.Events.Where(evt =>
                evt.Type == "startEvent" && evt.ProcessId != model.ProcessId).ToArray();
            Assert.NotEmpty(foreignStartEvents);
            Assert.DoesNotContain(foreignStartEvents, evt =>
                result.Any(entry => entry.Contains($"StartEvent: {evt.Id}", StringComparison.Ordinal)));
        }
    }
}
