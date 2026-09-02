using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Conformance
{
    public class C_2_0_Test
    {
        [Fact]
        public async Task Test_C_2_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.2.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var startEvent = model.Events.First(evt =>
                evt.Type == "startEvent" && evt.SubprocessId is null && evt.ProcessId == model.ProcessId);
            var result = engine.ExecuteFromStartEvent(model, startEvent.Id);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.2.0.bpmn");
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
        }
    }
}
