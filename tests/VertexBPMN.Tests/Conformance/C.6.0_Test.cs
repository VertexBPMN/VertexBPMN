using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Conformance
{
    public class C_6_0_Test
    {
        [Fact]
        public void Test_C_6_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.6.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var startEvent = model.Events.First(evt =>
                evt.Type == "startEvent" && evt.SubprocessId is null && evt.ProcessId == model.ProcessId);
            var result = engine.ExecuteFromStartEvent(model, startEvent.Id);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.6.0.bpmn");
            foreach (var item in result)
            {
                Console.WriteLine($"Result item: {item}");
            }
        }
    }
}
