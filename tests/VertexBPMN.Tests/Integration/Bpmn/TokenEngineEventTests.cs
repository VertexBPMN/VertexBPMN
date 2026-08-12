using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Integration.Bpmn
{
    public class TokenEngineEventTests
    {
        [Fact]
        public void Executes_TimerEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'><timerEventDefinition/></startEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
           Assert.Contains(trace, r => r.ToString().Contains("StartEvent: start1"));
           Assert.Contains(trace, r => r.ToString().Contains("SequenceFlow: f1"));
           Assert.Contains(trace, r => r.ToString().Contains("EndEvent: end1"));
        }

        [Fact]
        public void Executes_MessageEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P2'><startEvent id='start1'/><intermediateCatchEvent id='msg1'><messageEventDefinition/></intermediateCatchEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='msg1'/><sequenceFlow id='f2' sourceRef='msg1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
           Assert.Contains(trace, r => r.ToString().Contains("StartEvent: start1"));
           Assert.Contains(trace, r => r.ToString().Contains("SequenceFlow: f1"));
           Assert.Contains(trace, r => r.ToString().Contains("SequenceFlow: f2"));
           Assert.Contains(trace, r => r.ToString().Contains("EndEvent: end1"));
        }

        [Fact]
        public void Executes_SignalEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P3'><startEvent id='start1'/><intermediateThrowEvent id='sig1'><signalEventDefinition/></intermediateThrowEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='sig1'/><sequenceFlow id='f2' sourceRef='sig1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
           Assert.Contains(trace, r => r.ToString().Contains("StartEvent: start1"));
           Assert.Contains(trace, r => r.ToString().Contains("SequenceFlow: f1"));
           Assert.Contains(trace, r => r.ToString().Contains("SequenceFlow: f2"));
           Assert.Contains(trace, r => r.ToString().Contains("EndEvent: end1"));
        }

        [Fact]
        public void Executes_ConditionalEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P4'><startEvent id='start1'/><boundaryEvent id='cond1'><conditionalEventDefinition/></boundaryEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='cond1'/><sequenceFlow id='f2' sourceRef='cond1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
            var engine = new ProcessEngine();
            var trace = engine.Execute(model);
           Assert.Contains(trace, r => r.ToString().Contains("StartEvent: start1"));
           Assert.Contains(trace, r => r.ToString().Contains("SequenceFlow: f1"));
           Assert.Contains(trace, r => r.ToString().Contains("SequenceFlow: f2"));
           Assert.Contains(trace, r => r.ToString().Contains("EndEvent: end1"));
        }
    }
}
