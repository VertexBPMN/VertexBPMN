using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Conformance
{
    public class C_7_0_Test
    {
        //[Fact(Skip = "BPMN 2.0 C.7.0 test not implemented, is too complex and slow")]
        [Fact]
        public async Task Test_C_7_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.7.0.bpmn");
            var xml = await File.ReadAllTextAsync(bpmnFile, TestContext.Current.CancellationToken);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);

            var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);

            Assert.Equal(6, model.Tasks.Count);
            Assert.Equal(3, model.Gateways.Count);
            Assert.Equal(12, model.SequenceFlows.Count);
            Assert.Equal(3, model.DataObjects.Count);
            Assert.Equal(3, model.DataObjectReferences.Count);
            Assert.Contains(model.Tasks, task => task.Type == "businessRuleTask");
            Assert.Contains(model.Tasks, task => task.Type == "serviceTask");
            Assert.Contains(model.Tasks, task => task.Type == "userTask");

            var result = new ProcessEngine().Execute(model);
            Assert.Contains(result, entry => entry.Contains("StartEvent", StringComparison.Ordinal));
            Assert.Contains(result, entry => entry.Contains("UserTask", StringComparison.Ordinal));
        }
    }
}
