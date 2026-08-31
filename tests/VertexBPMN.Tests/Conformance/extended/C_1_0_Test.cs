using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_1_0_Test
    {
        [Fact]
        public async Task Test_C_1_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.1.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            model = model with
            {
                ProcessVariables = new Dictionary<string, object>
                {
                    ["approved"] = true,
                    ["clarified"] = "yes"
                }
            };
            var engine = new ProcessEngine();
            var startEvent = model.Events.First(evt =>
                evt.Type == "startEvent" && evt.SubprocessId is null && evt.ProcessId == model.ProcessId);
            var result = engine.ExecuteFromStartEvent(model, startEvent.Id);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.1.0.bpmn");
            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
        }
    }
}
