using System;
using System.Collections.Generic;
using VertexBPMN.Core.Bpmn;
using Xunit;

namespace VertexBPMN.Tests.Bpmn
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
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "esc1"),
                    new("f2", "esc1", "end1")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("SequenceFlow: f1", trace);
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("EndEvent: end1", trace);
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
