

using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Bpmn;

// <summary>
// Tests for Advanced BPMN 2.0 features: Boundary Events, Multi-Instance, Compensation, Transactions
// /Olympic-level validation of comprehensive BPMN execution capabilities
// </summary>
public class AdvancedBpmn20FeaturesTests
{
    [Fact]
    public void Handles_Interrupting_Boundary_Timer_Event()
    {
        // Test interrupting timer boundary event on user task
        var model = new BpmnModel(
            "P_Timer_Boundary",
            "Timer Boundary Event Process",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("timer_boundary", "boundaryEvent", new EventDefinition[]
                {
                    new TimerEventDefinition(null, "PT1M", null)
                }, null, new Dictionary<string, string>
                {
                    ["attachedToRef"] = "task1",
                    ["cancelActivity"] = "true"
                }),
                new("timeout_end", "endEvent"),
                new("normal_end", "endEvent")
            },
            new List<BpmnTask> { new("task1", "userTask") },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "task1"),
                new("flow2", "task1", "normal_end"),
                new("flow3", "timer_boundary", "timeout_end")
            },
            new List<BpmnSubprocess>()
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: start1"));
        Assert.Contains(trace, entry => entry.Contains("BoundaryEvent: timer_boundary"));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: timeout_end"));
    }

    [Fact]
    public void Handles_Sequential_Multi_Instance_Subprocess()
    {
        // Test sequential multi-instance subprocess with cardinality 3
        var model = new BpmnModel(
            "P_Sequential_MI",
            "Sequential Multi-Instance Process",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("end1", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "subprocess1"),
                new("flow2", "subprocess1", "end1")
            },
            new List<BpmnSubprocess> 
            { 
                new("subprocess1", false, false,
                    new MultiInstanceLoopCharacteristics(true, 3, null, null, null))  // Sequential MI with cardinality 3
            }
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: start1"));
        Assert.Contains(trace, entry => entry.Contains("Subprocess: subprocess1"));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: end1"));
    }

    [Fact]
    public void Handles_Parallel_Multi_Instance_Subprocess()
    {
        // Test parallel multi-instance subprocess with cardinality 2
        var model = new BpmnModel(
            "P_Parallel_MI",
            "Parallel Multi-Instance Process",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("end1", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "subprocess1"),
                new("flow2", "subprocess1", "end1")
            },
            new List<BpmnSubprocess> 
            { 
                new("subprocess1", false, false,
                    new MultiInstanceLoopCharacteristics(false, 2, null, null, null))  // Parallel MI with cardinality 2
            }
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: start1"));
        Assert.Contains(trace, entry => entry.Contains("Subprocess: subprocess1"));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: end1"));
    }

    [Fact]
    public void Handles_Transaction_Subprocess_With_Compensation()
    {
        // Test transaction subprocess with compensation boundary event
        var model = new BpmnModel(
            "P_Transaction_Comp",
            "Transaction with Compensation",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("comp_boundary", "boundaryEvent", new EventDefinition[]
                {
                    new CompensationEventDefinition(null)
                }, null, new Dictionary<string, string>
                {
                    ["attachedToRef"] = "tx_subprocess",
                    ["cancelActivity"] = "false",
                    ["isCompensation"] = "true"
                }),
                new("end1", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "tx_subprocess"),
                new("flow2", "tx_subprocess", "end1")
            },
            new List<BpmnSubprocess> 
            { 
                new("tx_subprocess", false, true)  // Transaction subprocess
            }
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: start1"));
        Assert.Contains(trace, entry => entry.Contains("BoundaryEvent: comp_boundary"));
        Assert.Contains(trace, entry => entry.Contains("Subprocess: tx_subprocess"));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: end1"));
    }

    [Fact]
    public void Handles_Non_Interrupting_Boundary_Message_Event()
    {
        // Test non-interrupting message boundary event
        var model = new BpmnModel(
            "P_NonInt_Message",
            "Non-Interrupting Message Boundary",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("msg_boundary", "boundaryEvent", new EventDefinition[]
                {
                    new MessageEventDefinition("message1", null)
                }, null, new Dictionary<string, string>
                {
                    ["attachedToRef"] = "task1",
                    ["cancelActivity"] = "false"
                }),  // Non-interrupting
                new("msg_end", "endEvent"),
                new("normal_end", "endEvent")
            },
            new List<BpmnTask> { new("task1", "userTask") },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "task1"),
                new("flow2", "task1", "normal_end"),
                new("flow3", "msg_boundary", "msg_end")
            },
            new List<BpmnSubprocess>()
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: start1"));
        Assert.Contains(trace, entry => entry.Contains("BoundaryEvent: msg_boundary"));
        Assert.Contains(trace, entry => entry.Contains("UserTask: task1"));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: normal_end"));
    }

    [Fact]
    public void Handles_Complex_Multi_Instance_With_Boundary_Events()
    {
        // Test complex scenario: Multi-instance subprocess with boundary events
        var model = new BpmnModel(
            "P_Complex_MI_Boundary",
            "Complex Multi-Instance with Boundary Events",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("error_boundary", "boundaryEvent", new EventDefinition[]
                {
                    new ErrorEventDefinition("error1")
                }, null, new Dictionary<string, string>
                {
                    ["attachedToRef"] = "mi_subprocess",
                    ["cancelActivity"] = "true"
                }),
                new("error_end", "endEvent"),
                new("normal_end", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "mi_subprocess"),
                new("flow2", "mi_subprocess", "normal_end"),
                new("flow3", "error_boundary", "error_end")
            },
            new List<BpmnSubprocess> 
            { 
                new("mi_subprocess", false, false,
                    new MultiInstanceLoopCharacteristics(false, 2, null, null, null))  // Parallel MI with cardinality 2
            }
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: start1"));
        Assert.Contains(trace, entry => entry.Contains("BoundaryEventSkipped: error_boundary"));
        Assert.Contains(trace, entry => entry.Contains("Subprocess: mi_subprocess"));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: normal_end"));
    }

    [Fact] 
    public void Handles_Event_Subprocess_Triggering()
    {
        // Test event subprocess (triggered by event)
        var model = new BpmnModel(
            "P_Event_Subprocess",
            "Event Subprocess Process",
            new List<BpmnEvent> 
            { 
                new("start1", "startEvent"),
                new("event_start", "startEvent"),
                new("event_end", "endEvent"),
                new("normal_end", "endEvent")
            },
            new List<BpmnTask> { new("task1", "userTask") },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow>
            {
                new("flow1", "start1", "task1"),
                new("flow2", "task1", "normal_end"),
                new("event_flow1", "event_start", "event_end")
            },
            new List<BpmnSubprocess> 
            { 
                new("event_subprocess", true)  // Event subprocess
            }
        );

        var engine = new ProcessEngine();
        var trace = engine.Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: start1"));
        Assert.Contains(trace, entry => entry.Contains("StartEvent: event_start"));
        Assert.Contains(trace, entry => entry.Contains("UserTask: task1"));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: normal_end"));
    }
}
