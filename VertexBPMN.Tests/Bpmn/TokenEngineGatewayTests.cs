using System;
using System.Collections.Generic;
using VertexBPMN.Core.Engine;
using Xunit;

namespace VertexBPMN.Tests.Bpmn
{
    public class TokenEngineGatewayTests
    {
        [Fact]
        public void Executes_EventBasedGateway_FlowsToFirstEvent()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1'><startEvent id='start1'/><eventBasedGateway id='eg1'/><intermediateCatchEvent id='msg1'><messageEventDefinition/></intermediateCatchEvent><sequenceFlow id='f1' sourceRef='start1' targetRef='eg1'/><sequenceFlow id='f2' sourceRef='eg1' targetRef='msg1'/><sequenceFlow id='f3' sourceRef='msg1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
            var parser = new BpmnParser();
            var model = parser.Parse(xml.Replace('\'', '"'));
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("StartEvent: start1", trace);
            Assert.Contains("SequenceFlow: f1", trace);
            Assert.Contains("SequenceFlow: f2", trace);
            Assert.Contains("SequenceFlow: f3", trace);
            Assert.Contains("EndEvent: end1", trace);
        }

        [Fact]
        public void Executes_ComplexGateway_FlowsToNext()
        {
            const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P2'><startEvent id='start1'/><complexGateway id='cg1'/><sequenceFlow id='f1' sourceRef='start1' targetRef='cg1'/><sequenceFlow id='f2' sourceRef='cg1' targetRef='end1'/><endEvent id='end1'/></process></definitions>";
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
