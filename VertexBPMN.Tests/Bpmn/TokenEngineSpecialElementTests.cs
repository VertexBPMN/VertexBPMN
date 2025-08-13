using System;
using System.Collections.Generic;
using VertexBPMN.Core.Bpmn;
using Xunit;

namespace VertexBPMN.Tests.Bpmn
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
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "call1"),
                    new("f2", "call1", "end1")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("UserTask: call1", trace); // callActivity mapped as UserTask for now
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("EndEvent: end1", trace);
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
                new List<BpmnSubprocess> { new("adhoc1", false) },
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "adhoc1"),
                    new("f2", "adhoc1", "end1")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("Subprocess: adhoc1", trace);
            Assert.Contains("SubprocessStart: adhoc1_start", trace);
            Assert.Contains("SubprocessEnd: adhoc1_end", trace);
            Assert.Contains("EndEvent: end1", trace);
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
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "end1")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("SequenceFlow: f1", trace);
            Assert.Contains("EndEvent: end1", trace);
        }
    }
}
