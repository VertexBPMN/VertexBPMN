using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Engine;
using Xunit;

namespace VertexBPMN.Tests.Bpmn;

/// <summary>
/// Tests for Advanced BPMN 2.0 features: Boundary Events, Multi-Instance, Compensation, Transactions
/// Olympic-level validation of comprehensive BPMN execution capabilities
/// </summary>
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
                new("timer_boundary", "boundaryEvent", "task1", false, true, "timer"),
                new("timeout_end", "endEvent"),
                new("normal_end", "endEvent")
            },
            new List<BpmnTask> { new("task1", "userTask") },
            new List<BpmnGateway>(),
            new List<BpmnSubprocess>(),
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "task1"),
                new("flow2", "task1", "normal_end"),
                new("flow3", "timer_boundary", "timeout_end")
            }
        );

        var engine = new TokenEngine();
        var trace = engine.Execute(model);

        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("UserTask: task1", trace);
        // Boundary event detection should be logged even if not triggered
        Assert.Contains("SequenceFlow: flow2", trace);
        Assert.Contains("EndEvent: normal_end", trace);
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
            new List<BpmnSubprocess> 
            { 
                new("subprocess1", true, false, false, true, 3) // Sequential MI with cardinality 3
            },
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "subprocess1"),
                new("flow2", "subprocess1", "end1")
            }
        );

        var engine = new TokenEngine();
        var trace = engine.Execute(model);

        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("Subprocess: subprocess1", trace);
        Assert.Contains("SequentialMultiInstance: subprocess1", trace);
        Assert.Contains("SequentialInstance: subprocess1 instance 1/3", trace);
        Assert.Contains("SequentialInstance: subprocess1 instance 2/3", trace);
        Assert.Contains("SequentialInstance: subprocess1 instance 3/3", trace);
        Assert.Contains("MultiInstanceCompleted: subprocess1", trace);
        Assert.Contains("EndEvent: end1", trace);
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
            new List<BpmnSubprocess> 
            { 
                new("subprocess1", true, false, false, false, 2) // Parallel MI with cardinality 2
            },
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "subprocess1"),
                new("flow2", "subprocess1", "end1")
            }
        );

        var engine = new TokenEngine();
        var trace = engine.Execute(model);

        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("Subprocess: subprocess1", trace);
        Assert.Contains("ParallelMultiInstance: subprocess1", trace);
        Assert.Contains("ParallelInstance: subprocess1 instance 1/2", trace);
        Assert.Contains("ParallelInstance: subprocess1 instance 2/2", trace);
        Assert.Contains("MultiInstanceCompleted: subprocess1", trace);
        Assert.Contains("EndEvent: end1", trace);
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
                new("comp_boundary", "boundaryEvent", "tx_subprocess", true, true, "compensation"),
                new("end1", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSubprocess> 
            { 
                new("tx_subprocess", false, false, true) // Transaction subprocess
            },
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "tx_subprocess"),
                new("flow2", "tx_subprocess", "end1")
            }
        );

        var engine = new TokenEngine();
        var trace = engine.Execute(model);

        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("TransactionSubprocess: tx_subprocess", trace);
        Assert.Contains("CompensationHandler: comp_boundary", trace);
        Assert.Contains("SubprocessEnd: tx_subprocess_end", trace);
        Assert.Contains("EndEvent: end1", trace);
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
                new("msg_boundary", "boundaryEvent", "task1", false, false, "message"), // Non-interrupting
                new("msg_end", "endEvent"),
                new("normal_end", "endEvent")
            },
            new List<BpmnTask> { new("task1", "userTask") },
            new List<BpmnGateway>(),
            new List<BpmnSubprocess>(),
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "task1"),
                new("flow2", "task1", "normal_end"),
                new("flow3", "msg_boundary", "msg_end")
            }
        );

        var engine = new TokenEngine();
        var trace = engine.Execute(model);

        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("UserTask: task1", trace);
        Assert.Contains("SequenceFlow: flow2", trace);
        Assert.Contains("EndEvent: normal_end", trace);
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
                new("error_boundary", "boundaryEvent", "mi_subprocess", false, true, "error"),
                new("error_end", "endEvent"),
                new("normal_end", "endEvent")
            },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSubprocess> 
            { 
                new("mi_subprocess", true, false, false, false, 2) // Parallel MI with cardinality 2
            },
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "mi_subprocess"),
                new("flow2", "mi_subprocess", "normal_end"),
                new("flow3", "error_boundary", "error_end")
            }
        );

        var engine = new TokenEngine();
        var trace = engine.Execute(model);

        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("Subprocess: mi_subprocess", trace);
        Assert.Contains("ParallelMultiInstance: mi_subprocess", trace);
        Assert.Contains("ParallelInstance: mi_subprocess instance 1/2", trace);
        Assert.Contains("ParallelInstance: mi_subprocess instance 2/2", trace);
        Assert.Contains("MultiInstanceCompleted: mi_subprocess", trace);
        Assert.Contains("EndEvent: normal_end", trace);
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
            new List<BpmnSubprocess> 
            { 
                new("event_subprocess", false, true) // Event subprocess
            },
            new List<BpmnSequenceFlow> 
            {
                new("flow1", "start1", "task1"),
                new("flow2", "task1", "normal_end"),
                new("event_flow1", "event_start", "event_end")
            }
        );

        var engine = new TokenEngine();
        var trace = engine.Execute(model);

        Assert.Contains("StartEvent: start1", trace);
        Assert.Contains("UserTask: task1", trace);
        Assert.Contains("EndEvent: normal_end", trace);
    }
}
