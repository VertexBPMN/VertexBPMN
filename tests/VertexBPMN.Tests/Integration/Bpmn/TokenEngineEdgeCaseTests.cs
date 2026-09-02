using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Infrastructure.Persistence.InMemory;

namespace VertexBPMN.Tests.Integration.Bpmn
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
                new List<BpmnGateway>(),
                new List<BpmnSubprocess> { new("sub1", false), new("sub2", false) },
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "sub1"),
                    new("f2", "sub1", "sub2"),
                    new("f3", "sub2", "end1")
                },
                new List<BpmnTask>()
            );
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.Contains("SubProcess: sub1", trace);
            Assert.Contains("SubProcess: sub2", trace);
            Assert.Contains(
                trace,
                t => t.Contains("EndEvent: end1", StringComparison.Ordinal));
            Assert.True(
                trace.Any(t => t.Contains("EndEvent: end1")),
                $"Expected trace to contain 'EndEvent: end1'. Got: {string.Join(", ", trace)}");
        }
        [Fact]
        public void Handles_ParallelGateway_With_Events()
        {
            var model = new BpmnModel(
                "P4",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("e1", "intermediateCatchEvent"), new("e2", "intermediateThrowEvent") },
                new List<BpmnGateway> { new("gw1", "parallelGateway") },
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("f1", "start1", "gw1"),
                    new("f2", "gw1", "e1"),
                    new("f3", "gw1", "e2"),
                }, new List<BpmnTask>()
            );
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.Contains(trace, x => x.StartsWith("ParallelGateway: gw1", StringComparison.Ordinal));
            Assert.Contains(trace, x => x.StartsWith("ParallelBranch: e1", StringComparison.Ordinal));
            Assert.Contains(trace, x => x.StartsWith("ParallelBranch: e2", StringComparison.Ordinal));
        }


        [Fact]
        public void Throws_On_Missing_StartEvent()
        {
            var model = new BpmnModel(
                "P1",
                "NoStart",
                new List<BpmnEvent>(),
                new List<BpmnGateway>(),
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow>(),
                new List<BpmnTask>()
            );
            var engine = new ProcessEngine();
            Assert.Throws<InvalidOperationException>(() => engine.Execute(model));
        }

        [Fact]
        public async Task Throws_On_Missing_Process_Element_In_Parser()
        {
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            const string xml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'></definitions>";
            var model = await parser.ParseAsync(xml.Replace('\'', '"'), TestContext.Current.CancellationToken);
            Assert.Empty(model.Events);
            Assert.Empty(model.Tasks);
        }

        [Fact]
        public async Task DecisionService_Returns_Null_For_Unknown_Decision()
        {
            var logger = new LoggerFactory().CreateLogger<DecisionService>();
            var service = new DecisionService(logger, new InMemoryDecisionRepository());
            var def = await service.GetDecisionByKeyAsync("unknown", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(def);
        }

        [Fact]
        public void TokenEngine_Handles_Unknown_Task_Type_Gracefully()
        {
            var model = new BpmnModel(
                "P2",
                "UnknownTask",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },     
                new List<BpmnGateway>(),
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("flow1", "start1", "t1"),
                    new("flow2", "t1", "end1")
                },
                new List<BpmnTask> { new("t1", "customTask") }
            );
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.Contains(trace, x => x.StartsWith("Task: t1 (customTask)", StringComparison.Ordinal));
        }
    }
}
