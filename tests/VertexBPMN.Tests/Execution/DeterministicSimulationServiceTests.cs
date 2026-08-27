using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Execution;

public sealed class DeterministicSimulationServiceTests
{
    private readonly DeterministicSimulationService _service = new(
        new BpmnParser(),
        NullLogger<DeterministicSimulationService>.Instance);

    [Fact]
    public async Task Parallel_split_and_join_emit_one_join_step()
    {
        const string bpmn = """
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="parallel">
                <startEvent id="start" />
                <parallelGateway id="split" />
                <task id="a" />
                <task id="b" />
                <parallelGateway id="join" />
                <endEvent id="end" />
                <sequenceFlow id="f1" sourceRef="start" targetRef="split" />
                <sequenceFlow id="f2" sourceRef="split" targetRef="a" />
                <sequenceFlow id="f3" sourceRef="split" targetRef="b" />
                <sequenceFlow id="f4" sourceRef="a" targetRef="join" />
                <sequenceFlow id="f5" sourceRef="b" targetRef="join" />
                <sequenceFlow id="f6" sourceRef="join" targetRef="end" />
              </process>
            </definitions>
            """;

        var result = await Simulate(bpmn, "parallel");

        Assert.True(result.Completed);
        Assert.Equal(new[] { "start", "split", "a", "b", "join", "end" }, Ids(result));
        Assert.Single(result.Steps.Where(step => step.ActivityId == "join"));
    }

    [Fact]
    public async Task Inclusive_join_does_not_wait_for_unselected_branch()
    {
        const string bpmn = """
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="inclusive">
                <startEvent id="start" />
                <inclusiveGateway id="split" default="toB" />
                <task id="a" />
                <task id="b" />
                <inclusiveGateway id="join" />
                <endEvent id="end" />
                <sequenceFlow id="f1" sourceRef="start" targetRef="split" />
                <sequenceFlow id="toA" sourceRef="split" targetRef="a">
                  <conditionExpression>takeA = true</conditionExpression>
                </sequenceFlow>
                <sequenceFlow id="toB" sourceRef="split" targetRef="b" />
                <sequenceFlow id="f4" sourceRef="a" targetRef="join" />
                <sequenceFlow id="f5" sourceRef="b" targetRef="join" />
                <sequenceFlow id="f6" sourceRef="join" targetRef="end" />
              </process>
            </definitions>
            """;

        var result = await Simulate(
            bpmn,
            "inclusive",
            new Dictionary<string, object> { ["takeA"] = true });

        Assert.True(result.Completed);
        Assert.Equal(new[] { "start", "split", "a", "join", "end" }, Ids(result));
    }

    [Fact]
    public async Task Embedded_subprocess_executes_its_internal_scope_before_parent_flow()
    {
        const string bpmn = """
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="subprocess">
                <startEvent id="start" />
                <subProcess id="sub" name="Embedded">
                  <startEvent id="innerStart" />
                  <task id="innerTask" />
                  <endEvent id="innerEnd" />
                  <sequenceFlow id="i1" sourceRef="innerStart" targetRef="innerTask" />
                  <sequenceFlow id="i2" sourceRef="innerTask" targetRef="innerEnd" />
                </subProcess>
                <endEvent id="end" />
                <sequenceFlow id="f1" sourceRef="start" targetRef="sub" />
                <sequenceFlow id="f2" sourceRef="sub" targetRef="end" />
              </process>
            </definitions>
            """;

        var result = await Simulate(bpmn, "subprocess");

        Assert.True(result.Completed);
        Assert.Equal(
            new[] { "start", "sub", "innerStart", "innerTask", "innerEnd", "end" },
            Ids(result));
    }

    [Fact]
    public async Task Event_based_gateway_requires_and_honors_explicit_event_selection()
    {
        const string bpmn = """
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="events">
                <startEvent id="start" />
                <eventBasedGateway id="wait" />
                <intermediateCatchEvent id="message"><messageEventDefinition /></intermediateCatchEvent>
                <intermediateCatchEvent id="timer"><timerEventDefinition><timeDuration>PT1M</timeDuration></timerEventDefinition></intermediateCatchEvent>
                <endEvent id="end" />
                <sequenceFlow id="f1" sourceRef="start" targetRef="wait" />
                <sequenceFlow id="messageFlow" sourceRef="wait" targetRef="message" />
                <sequenceFlow id="timerFlow" sourceRef="wait" targetRef="timer" />
                <sequenceFlow id="f4" sourceRef="message" targetRef="end" />
                <sequenceFlow id="f5" sourceRef="timer" targetRef="end" />
              </process>
            </definitions>
            """;

        var missingSelection = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Simulate(bpmn, "events"));
        Assert.Contains("EventSelections", missingSelection.Message);

        var result = await _service.SimulateAsync(new SimulationRequest
        {
            BpmnXml = bpmn,
            ProcessDefinitionId = "events",
            EventSelections = new Dictionary<string, string> { ["wait"] = "timer" }
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Completed);
        Assert.Equal(new[] { "start", "wait", "timer", "end" }, Ids(result));
    }

    [Fact]
    public async Task Cyclic_model_stops_at_max_steps_instead_of_running_unbounded()
    {
        const string bpmn = """
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
              <process id="cycle">
                <startEvent id="start" />
                <task id="loop" />
                <sequenceFlow id="f1" sourceRef="start" targetRef="loop" />
                <sequenceFlow id="f2" sourceRef="loop" targetRef="loop" />
              </process>
            </definitions>
            """;

        var result = await _service.SimulateAsync(new SimulationRequest
        {
            BpmnXml = bpmn,
            ProcessDefinitionId = "cycle",
            MaxSteps = 5
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Completed);
        Assert.Equal(5, result.Steps.Count);
        Assert.Contains("MaxSteps=5", result.Message);
    }

    private Task<SimulationResult> Simulate(
        string bpmn,
        string processId,
        Dictionary<string, object>? variables = null) =>
        _service.SimulateAsync(new SimulationRequest
        {
            BpmnXml = bpmn,
            ProcessDefinitionId = processId,
            Variables = variables ?? new Dictionary<string, object>()
        }, TestContext.Current.CancellationToken);

    private static string[] Ids(SimulationResult result) =>
        result.Steps.Select(step => step.ActivityId).ToArray();
}
