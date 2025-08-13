using System;
using System.Collections.Generic;
using VertexBPMN.Core.Bpmn;
using Xunit;

namespace VertexBPMN.Tests.Bpmn
{
    public class TokenEngineEventTests
    {
        [Fact]
        public void Executes_TimerEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'><timerEventDefinition/></startEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var parser = new BpmnParser();
            var model = parser.Parse(xml.Replace('\'', '"'));
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("SequenceFlow: f1", trace);
            Assert.Contains("EndEvent: end1", trace);
        }

        [Fact]
        public void Executes_MessageEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P2'><startEvent id='start1'/><intermediateCatchEvent id='msg1'><messageEventDefinition/></intermediateCatchEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='msg1'/><sequenceFlow id='f2' sourceRef='msg1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var parser = new BpmnParser();
            var model = parser.Parse(xml.Replace('\'', '"'));
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("SequenceFlow: f1", trace);
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("EndEvent: end1", trace);
        }

        [Fact]
        public void Executes_SignalEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P3'><startEvent id='start1'/><intermediateThrowEvent id='sig1'><signalEventDefinition/></intermediateThrowEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='sig1'/><sequenceFlow id='f2' sourceRef='sig1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var parser = new BpmnParser();
            var model = parser.Parse(xml.Replace('\'', '"'));
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("SequenceFlow: f1", trace);
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("EndEvent: end1", trace);
        }

        [Fact]
        public void Executes_ConditionalEvent_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P4'><startEvent id='start1'/><boundaryEvent id='cond1'><conditionalEventDefinition/></boundaryEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='cond1'/><sequenceFlow id='f2' sourceRef='cond1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var parser = new BpmnParser();
            var model = parser.Parse(xml.Replace('\'', '"'));
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("SequenceFlow: f1", trace);
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("EndEvent: end1", trace);
        }
    }
}
