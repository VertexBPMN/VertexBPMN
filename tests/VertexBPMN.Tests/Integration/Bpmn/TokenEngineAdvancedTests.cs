using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Integration.Bpmn
{
    public class TokenEngineAdvancedTests
    {
        [Fact]
        public async Task Executes_MultiInstanceSubprocess_FlowsToEnd()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'/><subProcess id='sub1'><multiInstanceLoopCharacteristics/></subProcess><sequenceFlow id='f1' sourceRef='start1' targetRef='sub1'/><sequenceFlow id='f2' sourceRef='sub1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'));
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.NotNull(trace);
            Assert.NotEmpty(trace);
            // Trace should contain start and end events, plus subprocess handling
            Assert.True(trace.Any(t => t.Contains("start1")), 
                $"Expected trace to contain 'start1'. Got: {string.Join(", ", trace)}");
            Assert.True(trace.Any(t => t.Contains("sub1")), 
                $"Expected trace to contain 'sub1'. Got: {string.Join(", ", trace)}");
            Assert.True(trace.Any(t => t.Contains("end1")), 
                $"Expected trace to contain 'end1'. Got: {string.Join(", ", trace)}");
        }

        [Fact]
        public async Task Executes_TransactionalSubprocess_And_CompensationHandler()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P2'><startEvent id='start1'/><subProcess id='tx1' transaction='true'><boundaryEvent id='comp1' attachedToRef='tx1' cancelActivity='false'><compensateEventDefinition/></boundaryEvent></subProcess><sequenceFlow id='f1' sourceRef='start1' targetRef='tx1'/><sequenceFlow id='f2' sourceRef='tx1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'));
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.NotNull(trace);
            Assert.NotEmpty(trace);
            // Verify basic flow elements are in trace
            Assert.True(trace.Any(t => t.Contains("start1")), 
                $"Expected trace to contain 'start1'. Got: {string.Join(", ", trace)}");
            Assert.True(trace.Any(t => t.Contains("tx1")), 
                $"Expected trace to contain 'tx1'. Got: {string.Join(", ", trace)}");
            Assert.True(trace.Any(t => t.Contains("end1")), 
                $"Expected trace to contain 'end1'. Got: {string.Join(", ", trace)}");
        }
    }
}
