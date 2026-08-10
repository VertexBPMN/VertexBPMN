

using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Bpmn;

/// <summary>
/// Tests for Advanced Gateway and Event-Driven Subprocess features
/// Olympic-level validation of comprehensive BPMN 2.0 gateway execution
/// </summary>
public class AdvancedGatewayAndEventTests
{
    [Fact]
    public void Handles_Complex_Gateway_With_Multiple_Flows()
    {
        // Test complex gateway with advanced condition evaluation
        var model = new BpmnModel(
            "P_Complex_Gateway",
            "Complex Gateway Process",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("end1", "endEvent"),
                new("end2", "endEvent"),
                new("end3", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway> { new("complex1", "complexGateway") },
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "complex1"),
                new("flow2", "complex1", "end1"),
                new("flow3", "complex1", "end2"),
                new("flow4", "complex1", "end3")
            },
            new List<BpmnSubprocess>()
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, r => r.ToString().Contains("StartEvent: start1"));
        Assert.Contains(trace, r => r.ToString().Contains("ComplexGateway: complex1"));
        Assert.Contains(trace, r => r.Contains("SequenceFlow: flow2"));
        Assert.Contains(trace, r => r.Contains("SequenceFlow: flow3"));
        Assert.Contains(trace, r => r.Contains("SequenceFlow: flow4"));
    }

    [Fact]
    public void Handles_Event_Based_Gateway_With_Message_Events()
    {
        var model = new BpmnModel(
            "P_Event_Gateway",
            "Event-Based Gateway Process",
            new List<BpmnEvent>
            {
                new("start1", "startEvent"),
                new("msg_event1", "intermediateCatchEvent", new EventDefinition[] { new MessageEventDefinition("message1", null) }),
                new("msg_event2", "intermediateCatchEvent", new EventDefinition[] { new MessageEventDefinition("message2", null) }),
                new("end1", "endEvent"),
                new("end2", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway> { new("event_gw1", "eventBasedGateway") },
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "event_gw1"),
                new("flow2", "event_gw1", "msg_event1"),
                new("flow3", "event_gw1", "msg_event2"),
                new("flow4", "msg_event1", "end1"),
                new("flow5", "msg_event2", "end2")
            },
            new List<BpmnSubprocess>());

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, r => r.Contains("StartEvent: start1"));
        Assert.Contains(trace, r => r.Contains("EventBasedGateway: event_gw1"));
        Assert.Contains(trace, r => r.Contains("intermediateCatchEvent: msg_event1"));
        Assert.Contains(trace, r => r.Contains("EndEvent: end1"));
    }

    [Fact]
    public void Handles_Message_Event_Subprocess()
    {
        var model = CreateEventSubprocessModel("message", new MessageEventDefinition("message1", null), "msg_subprocess", "msg_subprocess_start", "event_end", "task1");

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, r => r.Contains("IndexedEventSubprocessStart: msg_subprocess_start (message)"));
        Assert.Contains(trace, r => r.Contains("UserTask: task1"));
        Assert.Contains(trace, r => r.Contains("EndEvent: normal_end"));
        Assert.Contains(trace, r => r.Contains("EndEvent: event_end"));
    }

    [Fact]
    public void Handles_Error_Event_Subprocess()
    {
        var model = CreateEventSubprocessModel("error", new ErrorEventDefinition("error1"), "error_subprocess", "error_subprocess_start", "error_end", "risky_task");

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, r => r.Contains("IndexedEventSubprocessStart: error_subprocess_start (error)"));
        Assert.Contains(trace, r => r.Contains("ServiceTask: risky_task"));
        Assert.Contains(trace, r => r.Contains("EndEvent: normal_end"));
        Assert.Contains(trace, r => r.Contains("EndEvent: error_end"));
    }

    [Fact]
    public void Handles_Timer_Event_Subprocess()
    {
        var model = CreateEventSubprocessModel("timer", new TimerEventDefinition(null, "PT1M", null), "timer_subprocess", "timer_subprocess_start", "timer_end", "long_task");

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, r => r.Contains("IndexedEventSubprocessStart: timer_subprocess_start (timer)"));
        Assert.Contains(trace, r => r.Contains("UserTask: long_task"));
        Assert.Contains(trace, r => r.Contains("EndEvent: normal_end"));
        Assert.Contains(trace, r => r.Contains("EndEvent: timer_end"));
    }

    [Fact]
    public void Handles_Signal_Event_Subprocess()
    {
        var model = CreateEventSubprocessModel("signal", new SignalEventDefinition("signal1"), "signal_subprocess", "signal_subprocess_start", "signal_end", "waiting_task");

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, r => r.Contains("IndexedEventSubprocessStart: signal_subprocess_start (signal)"));
        Assert.Contains(trace, r => r.Contains("UserTask: waiting_task"));
        Assert.Contains(trace, r => r.Contains("EndEvent: normal_end"));
        Assert.Contains(trace, r => r.Contains("EndEvent: signal_end"));
    }

    [Fact]
    public void Handles_Mixed_Gateway_Types_In_Complex_Process()
    {
        var model = new BpmnModel(
            "P_Mixed_Gateways",
            "Mixed Gateway Types Process",
            new List<BpmnEvent>
            {
                new("start1", "startEvent"),
                new("timer_catch", "intermediateCatchEvent", new EventDefinition[] { new TimerEventDefinition(null, "PT1M", null) }),
                new("end1", "endEvent"), new("end2", "endEvent"), new("end3", "endEvent")
            },
            new List<BpmnTask> { new("prep_task", "userTask") },
            new List<BpmnGateway>
            {
                new("parallel1", "parallelGateway"),
                new("event_gw1", "eventBasedGateway"),
                new("exclusive1", "exclusiveGateway")
            },
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "prep_task"), new("flow2", "prep_task", "parallel1"),
                new("flow3", "parallel1", "event_gw1"), new("flow4", "parallel1", "exclusive1"),
                new("flow5", "event_gw1", "timer_catch"), new("flow6", "timer_catch", "end1"),
                new("flow7", "exclusive1", "end2"), new("flow8", "exclusive1", "end3")
            },
            new List<BpmnSubprocess>());

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, r => r.Contains("StartEvent: start1"));
        Assert.Contains(trace, r => r.Contains("UserTask: prep_task"));
        Assert.Contains(trace, r => r.Contains("ParallelGateway: parallel1"));
        Assert.Contains(trace, r => r.Contains("EventBasedGateway: event_gw1"));
        Assert.NotEmpty(trace);
    }

    private static BpmnModel CreateEventSubprocessModel(
        string eventType,
        EventDefinition definition,
        string subprocessId,
        string startId,
        string eventEndId,
        string taskId)
    {
        return new BpmnModel(
            $"P_{eventType}_Event_Sub",
            $"{eventType} Event Subprocess Process",
            new List<BpmnEvent>
            {
                new("start1", "startEvent"),
                new(startId, "startEvent", new[] { definition }, subprocessId),
                new(eventEndId, "endEvent", null, subprocessId),
                new("normal_end", "endEvent"),
            },
            new List<BpmnTask> { new(taskId, eventType == "error" ? "serviceTask" : "userTask", subprocessId) },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", taskId), new("flow2", taskId, "normal_end"),
                new("event_flow1", startId, eventEndId, false, null, subprocessId)
            },
            new List<BpmnSubprocess>
            {
                new(subprocessId, true, false, null, null, null,
                    new[] { startId, eventEndId, taskId }, new[] { "event_flow1" })
            });
    }

}
