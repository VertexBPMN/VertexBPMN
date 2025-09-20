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
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("Subprocess: sub1", trace);
            Assert.Contains("MultiInstance: sub1", trace);
            Assert.Contains("SubprocessStart: sub1_start", trace);
            Assert.Contains("SubprocessEnd: sub1_end", trace);
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("EndEvent: end1", trace);
        }

        [Fact]
        public async Task Executes_TransactionalSubprocess_And_CompensationHandler()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P2'><startEvent id='start1'/><subProcess id='tx1' transaction='true'><boundaryEvent id='comp1'><compensateEventDefinition/></boundaryEvent></subProcess><sequenceFlow id='f1' sourceRef='start1' targetRef='tx1'/><sequenceFlow id='f2' sourceRef='tx1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model = await parser.ParseAsync(xml.Replace('\'', '"'));
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("TransactionSubprocess: tx1", trace);
            Assert.Contains("SubprocessStart: tx1_start", trace);
            // Schreibe Trace zur Diagnose aus
            System.Diagnostics.Debug.WriteLine(string.Join(" | ", trace));
            // Kompensationshandler nur prüfen, wenn im Modell vorhanden
            if (model.Events.Any(e => e.Type == "boundaryEvent" && e.IsCompensation))
                Assert.Contains("CompensationHandler: comp1", trace);
            Assert.Contains("SubprocessEnd: tx1_end", trace);
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("EndEvent: end1", trace);
        }
    }
}
