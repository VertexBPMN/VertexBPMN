


using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Bpmn
{
    public class TokenEngineEventEdgeCaseTests
    {
        [Fact]
        public void Executes_EscalationEvent_FlowsToEnd()
        {
            var model = new BpmnModel(
                "P16",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("esc1", "intermediateThrowEvent"), new("end1", "endEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "esc1"),
                    new("f2", "esc1", "end1")
                },
                new List<BpmnSubprocess>()
            );
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.Contains(trace, x => x.StartsWith("StartEvent: start1", StringComparison.Ordinal));
            Assert.Contains(trace, x => x.StartsWith("SequenceFlow: f1", StringComparison.Ordinal));
            Assert.Contains(trace, x => x.StartsWith("SequenceFlow: f2", StringComparison.Ordinal));
            Assert.Contains(trace, x => x.StartsWith("EndEvent: end1", StringComparison.Ordinal));
        }

        [Fact]
        public void Executes_ErrorEndEvent()
        {
            var model = new BpmnModel(
                "P18",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "end1")
                },
                new List<BpmnSubprocess>()
            );
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.Contains(trace, x => x.StartsWith("StartEvent: start1", StringComparison.Ordinal));
            Assert.Contains(trace, x => x.StartsWith("SequenceFlow: f1", StringComparison.Ordinal));
            Assert.Contains(trace, x => x.StartsWith("EndEvent: end1", StringComparison.Ordinal));
        }
    }
}
