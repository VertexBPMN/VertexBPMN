using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Services;
using Xunit;

namespace VertexBPMN.Tests.Bpmn
{
    public class TokenEngineEdgeCaseTests
    {
        [Fact]
        public void Handles_Nested_Subprocesses()
        {
            var model = new BpmnModel(
                "P3",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway>(),
                new List<BpmnSubprocess> { new("sub1", false), new("sub2", false) },
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "sub1"),
                    new("f2", "sub1", "sub2"),
                    new("f3", "sub2", "end1")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("Subprocess: sub1", trace);
            Assert.Contains("Subprocess: sub2", trace);
            Assert.Contains("EndEvent: end1", trace);
        }
        [Fact]
        public void Handles_ParallelGateway_With_Events()
        {
            var model = new BpmnModel(
                "P4",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("e1", "intermediateCatchEvent"), new("e2", "intermediateThrowEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway> { new("gw1", "parallelGateway") },
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "gw1"),
                    new("f2", "gw1", "e1"),
                    new("f3", "gw1", "e2"),
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("ParallelGateway: gw1", trace);
            Assert.Contains("ParallelBranch: e1", trace);
            Assert.Contains("ParallelBranch: e2", trace);
        }


        [Fact]
        public void Throws_On_Missing_StartEvent()
        {
            var model = new BpmnModel(
                "P1",
                "NoStart",
                new List<BpmnEvent>(),
                new List<BpmnTask>(),
                new List<BpmnGateway>(),
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow>()
            );
            var engine = new TokenEngine();
            Assert.Throws<InvalidOperationException>(() => engine.Execute(model));
        }

        [Fact]
        public void Throws_On_Missing_Process_Element_In_Parser()
        {
            var parser = new BpmnParser();
            const string xml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'></definitions>";
            Assert.Throws<InvalidOperationException>(() => parser.Parse(xml.Replace("'", "\"")));
        }

        [Fact]
        public async Task DecisionService_Returns_Null_For_Unknown_Decision()
        {
            var service = new DecisionService();
            var def = await service.GetDecisionByKeyAsync("unknown");
            Assert.Null(def);
        }

        [Fact]
        public void TokenEngine_Handles_Unknown_Task_Type_Gracefully()
        {
            var model = new BpmnModel(
                "P2",
                "UnknownTask",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
                new List<BpmnTask> { new("t1", "customTask") },
                new List<BpmnGateway>(),
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("flow1", "start1", "t1"),
                    new("flow2", "t1", "end1")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("UserTask: t1", trace); // Falls back to UserTask
        }
    }
}
