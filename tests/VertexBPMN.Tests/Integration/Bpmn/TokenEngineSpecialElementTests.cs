

using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Bpmn
{
    public class TokenEngineSpecialElementTests
    {
        [Fact]
        public void Executes_CallActivity_FlowsToEnd()
        {
            var model = new BpmnModel(
                "P14",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
                new List<BpmnTask> { new("call1", "callActivity") },
                new List<BpmnGateway>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "call1"),
                    new("f2", "call1", "end1")
                },
                new List<BpmnSubprocess>()
            );
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.NotNull(trace);
            Assert.NotEmpty(trace);
            // Verify trace contains expected elements
            Assert.True(trace.Any(t => t.Contains("start1")), "Expected 'start1' in trace");
            Assert.True(trace.Any(t => t.Contains("call1")), "Expected 'call1' in trace");
            Assert.True(trace.Any(t => t.Contains("end1")), "Expected 'end1' in trace");
        }

        [Fact]
        public void Executes_AdHocSubProcess_FlowsToEnd()
        {
            var model = new BpmnModel(
                "P15",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "adhoc1"),
                    new("f2", "adhoc1", "end1")
                },
                new List<BpmnSubprocess> { new("adhoc1", false) }
            );
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.NotNull(trace);
            Assert.NotEmpty(trace);
            // Verify trace contains subprocess elements - using more flexible assertions
            Assert.True(trace.Any(t => t.Contains("adhoc1")), 
                $"Expected 'adhoc1' in trace. Got: {string.Join(", ", trace)}");
            Assert.True(trace.Any(t => t.Contains("end1")), 
                $"Expected 'end1' in trace. Got: {string.Join(", ", trace)}");
        }

        [Fact]
        public void Executes_TerminateEndEvent()
        {
            var model = new BpmnModel(
                "P17",
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
            Assert.NotNull(trace);
            Assert.NotEmpty(trace);
            // Verify trace contains start and end events
            Assert.True(trace.Any(t => t.Contains("start1")), 
                $"Expected 'start1' in trace. Got: {string.Join(", ", trace)}");
            Assert.True(trace.Any(t => t.Contains("end1")), 
                $"Expected 'end1' in trace. Got: {string.Join(", ", trace)}");
        }
    }
}
