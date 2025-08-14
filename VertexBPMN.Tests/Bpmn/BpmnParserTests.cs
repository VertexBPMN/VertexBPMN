using System;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Engine;
using Xunit;

namespace VertexBPMN.Tests.Bpmn;

public class BpmnParserTests
{
    [Fact]
    public void Parses_CallActivity()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P14'><callActivity id='call1' calledElement='OtherProcess'/></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Single(model.Tasks, t => t.Type == "callActivity" && t.Id == "call1");
    }

    [Fact]
    public void Parses_AdHocSubProcess()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P15'><adHocSubProcess id='adhoc1'/></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Single(model.Subprocesses, s => s.Id == "adhoc1");
    }

    [Fact]
    public void Parses_EscalationEvent()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P16'><intermediateThrowEvent id='esc1'><escalationEventDefinition/></intermediateThrowEvent></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Contains(model.Events, e => e.Type == "intermediateThrowEvent" && e.Id == "esc1");
    }

    [Fact]
    public void Parses_TerminateEndEvent()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P17'><endEvent id='end1'><terminateEventDefinition/></endEvent></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Contains(model.Events, e => e.Type == "endEvent" && e.Id == "end1");
    }

    [Fact]
    public void Parses_ErrorEndEvent()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P18'><endEvent id='end1'><errorEventDefinition/></endEvent></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Contains(model.Events, e => e.Type == "endEvent" && e.Id == "end1");
    }
    [Fact]
    public void Parses_Intermediate_Events()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P8'><intermediateCatchEvent id='ice1'/><intermediateThrowEvent id='ite1'/></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace("'", "\""));
        Assert.Single(model.Events, e => e.Type == "intermediateCatchEvent");
        Assert.Single(model.Events, e => e.Type == "intermediateThrowEvent");
    }

    [Fact]
    public void Parses_Event_Subprocess()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P9'><subProcess id='esp1' triggeredByEvent='true'></subProcess></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace("'", "\""));
        Assert.Single(model.Subprocesses, s => s.IsEventSubprocess);
        Assert.Equal("esp1", model.Subprocesses.First(s => s.IsEventSubprocess).Id);
    }

    [Fact]
    public void Parses_Boundary_Event()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P7'><userTask id='task1'/><boundaryEvent id='b1' attachedToRef='task1'/></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace("'", "\""));
        var boundary = model.Events.FirstOrDefault(e => e.Type == "boundaryEvent");
        Assert.NotNull(boundary);
        Assert.Equal("b1", boundary.Id);
        Assert.Equal("task1", boundary.AttachedToRef);
    }

    [Fact]
    public void Parses_Basic_Process()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1' name='Test'></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace("'", "\""));
        Assert.Equal("P1", model.Id);
        Assert.Equal("Test", model.Name);
        Assert.Empty(model.Events);
        Assert.Empty(model.Tasks);
        Assert.Empty(model.Gateways);
        Assert.Empty(model.SequenceFlows);
    }

    [Fact]
    public void Parses_Events_And_SequenceFlows()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P2'><startEvent id='start1'/><endEvent id='end1'/><sequenceFlow id='flow1' sourceRef='start1' targetRef='end1'/></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace("'", "\""));
        Assert.Single(model.Events, e => e.Type == "startEvent");
        Assert.Single(model.Events, e => e.Type == "endEvent");
        Assert.Single(model.SequenceFlows);
        Assert.Equal("start1", model.Events.First(e => e.Type == "startEvent").Id);
        Assert.Equal("end1", model.Events.First(e => e.Type == "endEvent").Id);
        Assert.Equal("flow1", model.SequenceFlows[0].Id);
    }

    [Fact]
    public void Parses_Timer_Message_Signal_Conditional_Events()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P10'>
            <startEvent id='start1'><timerEventDefinition/></startEvent>
            <intermediateCatchEvent id='msg1'><messageEventDefinition/></intermediateCatchEvent>
            <intermediateThrowEvent id='sig1'><signalEventDefinition/></intermediateThrowEvent>
            <boundaryEvent id='cond1'><conditionalEventDefinition/></boundaryEvent>
        </process></definitions>";
        var parser = new BpmnParser();
    var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Contains(model.Events, e => e.Type == "startEvent" && e.Id == "start1");
        Assert.Contains(model.Events, e => e.Type == "intermediateCatchEvent" && e.Id == "msg1");
        Assert.Contains(model.Events, e => e.Type == "intermediateThrowEvent" && e.Id == "sig1");
        Assert.Contains(model.Events, e => e.Type == "boundaryEvent" && e.Id == "cond1");
    }

    [Fact]
    public void Parses_EventBasedGateway()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P11'><eventBasedGateway id='eg1'/></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Single(model.Gateways, g => g.Type == "eventBasedGateway" && g.Id == "eg1");
    }

    [Fact]
    public void Parses_ComplexGateway()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P12'><complexGateway id='cg1'/></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Single(model.Gateways, g => g.Type == "complexGateway" && g.Id == "cg1");
    }

    [Fact]
    public void Parses_TransactionalSubprocess_And_CompensationHandler()
    {
    const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P13'><subProcess id='tx1' triggeredByEvent='false' transaction='true'/><boundaryEvent id='comp1' attachedToRef='tx1'><compensateEventDefinition/></boundaryEvent></process></definitions>";
        var parser = new BpmnParser();
        var model = parser.Parse(xml.Replace('\'', '"'));
        Assert.Single(model.Subprocesses, s => s.Id == "tx1");
        Assert.Contains(model.Events, e => e.Type == "boundaryEvent" && e.Id == "comp1");
    }
}
    public class TokenEngineTests
    {
        [Fact]
        public void Executes_BusinessRuleTask_With_DecisionService()
        {
            var model = new BpmnModel(
                "P10",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
                new List<BpmnTask> { new("brt1", "businessRuleTask") },
                new List<BpmnGateway>(),
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                    new("flow1", "start1", "brt1"),
                    new("flow2", "brt1", "end1")
                }
            );
            var engine = new TokenEngine();
            var decisionService = new VertexBPMN.Core.Services.DecisionService();
            var trace = engine.Execute(model, decisionService);
            Assert.Contains("BusinessRuleTask: brt1", trace);
            Assert.Contains("DecisionEvaluated: brt1 => 1", trace);
        }
    
        [Fact]
        public void Executes_ParallelGateway_Flow()
        {
            var model = new BpmnModel(
                "P4",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway> { new("gw1", "parallelGateway") },
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                new("flow1", "start1", "gw1"),
                new("flow2", "gw1", "t1"),
                new("flow3", "gw1", "t2")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("ParallelGateway: gw1", trace);
            Assert.Contains("ParallelBranch: t1", trace);
            Assert.Contains("ParallelBranch: t2", trace);
        }

        [Fact]
        public void Executes_InclusiveGateway_Flow()
        {
            var model = new BpmnModel(
                "P5",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway> { new("gw1", "inclusiveGateway") },
                new List<BpmnSubprocess>(),
                new List<BpmnSequenceFlow> {
                new("flow1", "start1", "gw1"),
                new("flow2", "gw1", "t1"),
                new("flow3", "gw1", "t2")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("InclusiveGateway: gw1", trace);
            Assert.Contains("InclusiveBranch: t1", trace);
            Assert.Contains("InclusiveBranch: t2", trace);
        }

        [Fact]
        public void Executes_Subprocess_And_MultiInstance()
        {
            var model = new BpmnModel(
                "P6",
                "Test",
                new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
                new List<BpmnTask>(),
                new List<BpmnGateway>(),
                new List<BpmnSubprocess> { new("sub1", true) },
                new List<BpmnSequenceFlow> {
                new("flow1", "start1", "sub1"),
                new("flow2", "sub1", "end1")
                }
            );
            var engine = new TokenEngine();
            var trace = engine.Execute(model);
            Assert.Contains("Subprocess: sub1", trace);
            Assert.Contains("MultiInstance: sub1", trace);
            Assert.Contains("SubprocessStart: sub1_start", trace);
            Assert.Contains("SubprocessEnd: sub1_end", trace);
        }

    }

public class DecisionServiceTests
{
    [Fact]
    public async Task Evaluate_Decision_Returns_Inputs_As_Outputs()
    {
        var service = new VertexBPMN.Core.Services.DecisionService();
        var inputs = new Dictionary<string, object> { { "foo", 42 } };
        var result = await service.EvaluateDecisionByKeyAsync("test", inputs);
        Assert.NotNull(result);
        Assert.True(result.Outputs.ContainsKey("foo"));
        Assert.Equal(42, (result.Outputs["foo"] as int?) ?? ((System.Text.Json.JsonElement)result.Outputs["foo"]).GetInt32());
    }
}

