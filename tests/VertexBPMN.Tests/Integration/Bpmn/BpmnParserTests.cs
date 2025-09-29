using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Engine.Parsing;


namespace VertexBPMN.Tests.Integration.Bpmn;

public class BpmnParserTests
{
    [Fact]
    public void Parses_CallActivity()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P14'><callActivity id='call1' calledElement='OtherProcess'/></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Single(model.Tasks, t => t.Type == "callActivity" && t.Id == "call1");
    }

    [Fact]
    public void Parses_AdHocSubProcess()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P15'><adHocSubProcess id='adhoc1'/></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Single(model.Subprocesses, s => s.Id == "adhoc1");
    }

    [Fact]
    public void Parses_EscalationEvent()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P16'><intermediateThrowEvent id='esc1'><escalationEventDefinition/></intermediateThrowEvent></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Contains(model.Events, e => e.Type == "intermediateThrowEvent" && e.Id == "esc1");
    }

    [Fact]
    public void Parses_TerminateEndEvent()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P17'><endEvent id='end1'><terminateEventDefinition/></endEvent></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Contains(model.Events, e => e.Type == "endEvent" && e.Id == "end1");
    }

    [Fact]
    public void Parses_ErrorEndEvent()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P18'><endEvent id='end1'><errorEventDefinition/></endEvent></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Contains(model.Events, e => e.Type == "endEvent" && e.Id == "end1");
    }
    [Fact]
    public void Parses_Intermediate_Events()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P8'><intermediateCatchEvent id='ice1'/><intermediateThrowEvent id='ite1'/></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Single(model.Events, e => e.Type == "intermediateCatchEvent");
        Assert.Single(model.Events, e => e.Type == "intermediateThrowEvent");
    }

    [Fact]
    public void Parses_Event_Subprocess()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P9'><subProcess id='esp1' triggeredByEvent='true'></subProcess></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Single(model.Subprocesses, s => s.IsEventSubprocess);
        Assert.Equal("esp1", model.Subprocesses.First(s => s.IsEventSubprocess).Id);
    }

    [Fact]
    public void Parses_Boundary_Event()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P7'><userTask id='task1'/><boundaryEvent id='b1' attachedToRef='task1'/></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
        var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        var boundary = model.Events.FirstOrDefault(e => e.Type == "boundaryEvent");
        Assert.NotNull(boundary);
        Assert.Equal("b1", boundary.Id);
        Assert.Equal("task1", boundary.AttachedToRef);
    }

    [Fact]
    public void Parses_Basic_Process()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P1' name='Test'></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
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
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
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
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
     var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Contains(model.Events, e => e.Type == "startEvent" && e.Id == "start1");
        Assert.Contains(model.Events, e => e.Type == "intermediateCatchEvent" && e.Id == "msg1");
        Assert.Contains(model.Events, e => e.Type == "intermediateThrowEvent" && e.Id == "sig1");
        Assert.Contains(model.Events, e => e.Type == "boundaryEvent" && e.Id == "cond1");
    }

    [Fact]
    public void Parses_EventBasedGateway()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P11'><eventBasedGateway id='eg1'/></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Single(model.Gateways, g => g.Type == "eventBasedGateway" && g.Id == "eg1");
    }

    [Fact]
    public void Parses_ComplexGateway()
    {
        const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P12'><complexGateway id='cg1'/></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Single(model.Gateways, g => g.Type == "complexGateway" && g.Id == "cg1");
    }

    [Fact]
    public void Parses_TransactionalSubprocess_And_CompensationHandler()
    {
    const string xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='P13'><subProcess id='tx1' triggeredByEvent='false' transaction='true'/><boundaryEvent id='comp1' attachedToRef='tx1'><compensateEventDefinition/></boundaryEvent></process></definitions>";
        var logger = new Mock<ILogger<BpmnParser>>();var parser = new BpmnParser(logger.Object, TracerProvider.Default);
         var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
        Assert.Single(model.Subprocesses, s => s.Id == "tx1");
        Assert.Contains(model.Events, e => e.Type == "boundaryEvent" && e.Id == "comp1");
    }
}