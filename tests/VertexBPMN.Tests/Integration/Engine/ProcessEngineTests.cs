

using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence.InMemory;
using Moq;

namespace VertexBPMN.Tests.Integration.Engine;

public class ProcessEngineTests
{
    [Fact]
    public async Task Registers_And_Executes_Model_By_ProcessId()
    {
        var model = CreateSimpleModel("registered-process");
        var engine = new ProcessEngine();

        engine.RegisterBpmnModel("registered-process", model);
        var trace = await engine.ExecuteProcessAsync("registered-process", TestContext.Current.CancellationToken);

        Assert.Contains(trace, x => x.Contains("StartEvent: start1"));
        Assert.Contains(trace, x => x.Contains("EndEvent: end1"));
    }

    [Fact]
    public async Task ReRegistering_ProcessId_Replaces_Model()
    {
        var engine = new ProcessEngine();
        engine.RegisterBpmnModel("replaceable", CreateSimpleModel("first"));
        engine.RegisterBpmnModel("replaceable", CreateSimpleModel("second"));

        var trace = await engine.ExecuteProcessAsync("replaceable", TestContext.Current.CancellationToken);

        Assert.Contains(trace, x => x.Contains("StartEvent: start1"));
        Assert.Contains(trace, x => x.Contains("EndEvent: end1"));
        Assert.DoesNotContain(trace, x => x.Contains("first"));
    }

    [Fact]
    public async Task Registers_BpmnXml_Through_Injected_Parser()
    {
        var model = CreateSimpleModel("xml-process");
        var parser = new Mock<IBpmnParser>();
        parser.Setup(x => x.ParseAsync("<definitions />", It.IsAny<CancellationToken>()))
            .ReturnsAsync(model);
        var engine = new ProcessEngine(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessEngine>.Instance,
            NullServiceTaskRegistry.Instance,
            bpmnParser: parser.Object);

        await engine.RegisterBpmnModelAsync("xml-process", "<definitions />", TestContext.Current.CancellationToken);
        var trace = await engine.ExecuteProcessAsync("xml-process", TestContext.Current.CancellationToken);

        Assert.Contains(trace, x => x.Contains("EndEvent: end1"));
        parser.Verify(x => x.ParseAsync("<definitions />", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executing_Unknown_ProcessId_Throws()
    {
        var engine = new ProcessEngine();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => engine.ExecuteProcessAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Stores_Completed_Execution_History_By_ExecutionId()
    {
        var engine = new ProcessEngine();

        var trace = await engine.ExecuteAsync(CreateSimpleModel("history-process"), TestContext.Current.CancellationToken);

        Assert.NotNull(engine.LastExecutionId);
        Assert.True(engine.TryGetExecutionHistory(engine.LastExecutionId!, out var history));
        Assert.NotNull(history);
        Assert.Equal("history-process", history!.ProcessId);
        Assert.Equal(trace, history.Trace);
        Assert.NotEmpty(history.History);
    }

    [Fact]
    public async Task Evaluates_Registered_Local_Dmn_For_BusinessRuleTask()
    {
        var parser = new Mock<IDmnParser>();
        parser.Setup(x => x.ParseAsync("<definitions />", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DmnDecision("decision-1", "Decision", [], [], [], "UNIQUE"));
        var dmnEngine = new Mock<IDmnEngine>();
        dmnEngine.Setup(x => x.EvaluateDecisionAsync(
                It.IsAny<DmnDecision>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object> { ["result"] = "approved" });
        var engine = new ProcessEngine(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessEngine>.Instance,
            NullServiceTaskRegistry.Instance,
            bpmnParser: null,
            dmnParser: parser.Object,
            dmnEngine: dmnEngine.Object);
        var model = new BpmnModel(
            "dmn-process",
            "Test",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask> { new("decision-task", "businessRuleTask", null, new Dictionary<string, string> { ["decisionRef"] = "decision-1" }) },
            [],
            [new BpmnSequenceFlow("flow1", "start1", "decision-task"), new BpmnSequenceFlow("flow2", "decision-task", "end1")],
            []);

        await engine.RegisterDmnModelAsync("decision-1", "<definitions />");
        var trace = await engine.ExecuteAsync(model, TestContext.Current.CancellationToken);

        Assert.Contains(trace, x => x.Contains("DecisionEvaluated: decision-1 (local)"));
        dmnEngine.Verify(x => x.EvaluateDecisionAsync(
            It.IsAny<DmnDecision>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Executes_BusinessRuleTask_With_DecisionService()
    {
        var model = new BpmnModel(
            "P10",
            "Test",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask> { new("brt1", "businessRuleTask") },
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "brt1"),
                new("flow2", "brt1", "end1")
            },
            new List<BpmnSubprocess>()
        );
        var engine = new ProcessEngine();
        var logger = new LoggerFactory().CreateLogger<DecisionService>();
        var decisionService = new DecisionService(logger, new InMemoryDecisionRepository());
        var trace = engine.Execute(model, decisionService);
        Assert.Contains(trace, x => x.Contains("BusinessRuleTask: brt1"));
       // Assert.Contains(trace, x => x.Contains("DecisionEvaluated: brt1 => 1"));
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
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "gw1"),
                new("flow2", "gw1", "t1"),
                new("flow3", "gw1", "t2")
            },
            new List<BpmnSubprocess>()
        );
        var engine = new ProcessEngine();
        var trace = engine.Execute(model);
        Assert.Contains(trace, x => x.Contains("ParallelGateway: gw1"));
        Assert.Contains(trace, x => x.Contains("ParallelBranch: t1"));
        Assert.Contains(trace, x => x.Contains("ParallelBranch: t2"));
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
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "gw1"),
                new("flow2", "gw1", "t1"),
                new("flow3", "gw1", "t2")
            },
            new List<BpmnSubprocess>()
        );
        var engine = new ProcessEngine();
        var trace = engine.Execute(model);
        Assert.Contains(trace, x => x.Contains("InclusiveGateway: gw1"));
        Assert.Contains(trace, x => x.Contains("InclusiveBranch: t1"));
        Assert.Contains(trace, x => x.Contains("InclusiveBranch: t2"));
    }

    [Fact]
    public void ExclusiveGateway_Selects_Matching_Condition_Instead_Of_Default()
    {
        var model = new BpmnModel(
            "exclusive-conditions",
            "Exclusive conditions",
            Events:
            [
                new BpmnEvent("start", "startEvent"),
                new BpmnEvent("approved-end", "endEvent"),
                new BpmnEvent("rejected-end", "endEvent")
            ],
            Gateways: [new BpmnGateway("decision", "exclusiveGateway", "to-rejected")],
            Subprocesses: [],
            SequenceFlows:
            [
                new BpmnSequenceFlow("to-decision", "start", "decision"),
                new BpmnSequenceFlow("to-approved", "decision", "approved-end", ConditionExpression: "${score >= 80}"),
                new BpmnSequenceFlow("to-rejected", "decision", "rejected-end", IsDefault: true)
            ],
            Tasks: [],
            ProcessVariables: new Dictionary<string, object> { ["score"] = 90 });

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, entry => entry.Contains("ExclusiveFlowSelected: to-approved", StringComparison.Ordinal));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: approved-end", StringComparison.Ordinal));
        Assert.DoesNotContain(trace, entry => entry.Contains("EndEvent: rejected-end", StringComparison.Ordinal));
    }

    [Fact]
    public void ExclusiveGateway_Without_Match_Or_Default_Throws()
    {
        var model = new BpmnModel(
            "exclusive-no-route",
            "Exclusive no route",
            Events: [new BpmnEvent("start", "startEvent"), new BpmnEvent("end", "endEvent")],
            Gateways: [new BpmnGateway("decision", "exclusiveGateway")],
            Subprocesses: [],
            SequenceFlows:
            [
                new BpmnSequenceFlow("to-decision", "start", "decision"),
                new BpmnSequenceFlow("conditional", "decision", "end", ConditionExpression: "${score >= 80}")
            ],
            Tasks: [],
            ProcessVariables: new Dictionary<string, object> { ["score"] = 10 });

        var exception = Assert.Throws<InvalidOperationException>(() => new ProcessEngine().Execute(model));

        Assert.Contains("no matching outgoing Sequence Flow", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_With_Instance_Variables_Does_Not_Mutate_Or_Leak_Into_Next_Execution()
    {
        var model = new BpmnModel(
            "isolated-runtime-variables",
            "Isolated runtime variables",
            Events: [new BpmnEvent("start", "startEvent"), new BpmnEvent("end", "endEvent")],
            Gateways: [new BpmnGateway("decision", "exclusiveGateway")],
            Subprocesses: [],
            SequenceFlows:
            [
                new BpmnSequenceFlow("to-decision", "start", "decision"),
                new BpmnSequenceFlow("approved", "decision", "end", ConditionExpression: "${approved}")
            ],
            Tasks: []);
        var engine = new ProcessEngine();

        var trace = engine.Execute(model, new Dictionary<string, object> { ["approved"] = true });

        Assert.Contains(trace, entry => entry.Contains("EndEvent: end", StringComparison.Ordinal));
        Assert.Null(model.ProcessVariables);
        var exception = Assert.Throws<InvalidOperationException>(() => engine.Execute(model));
        Assert.Contains("available process variables", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InclusiveGateway_Activates_Only_Matching_Branches()
    {
        var model = new BpmnModel(
            "inclusive-conditions",
            "Inclusive conditions",
            Events:
            [
                new BpmnEvent("start", "startEvent"),
                new BpmnEvent("email-end", "endEvent"),
                new BpmnEvent("sms-end", "endEvent"),
                new BpmnEvent("default-end", "endEvent")
            ],
            Gateways: [new BpmnGateway("channels", "inclusiveGateway", "default-channel")],
            Subprocesses: [],
            SequenceFlows:
            [
                new BpmnSequenceFlow("to-channels", "start", "channels"),
                new BpmnSequenceFlow("email-channel", "channels", "email-end", ConditionExpression: "${email == true}"),
                new BpmnSequenceFlow("sms-channel", "channels", "sms-end", ConditionExpression: "${sms == true}"),
                new BpmnSequenceFlow("default-channel", "channels", "default-end", IsDefault: true)
            ],
            Tasks: [],
            ProcessVariables: new Dictionary<string, object> { ["email"] = true, ["sms"] = false });

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, entry => entry.Contains("InclusiveBranch: email-end via email-channel", StringComparison.Ordinal));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: email-end", StringComparison.Ordinal));
        Assert.DoesNotContain(trace, entry => entry.Contains("EndEvent: sms-end", StringComparison.Ordinal));
        Assert.DoesNotContain(trace, entry => entry.Contains("EndEvent: default-end", StringComparison.Ordinal));
    }

    [Fact]
    public void ExclusiveGateway_Evaluates_Declared_Feel_Quantifier()
    {
        var feelAttributes = new Dictionary<string, string>
        {
            ["conditionExpressionLanguage"] = "https://www.omg.org/spec/DMN/20191111/FEEL/"
        };
        var model = new BpmnModel(
            "exclusive-feel",
            "Exclusive FEEL",
            Events:
            [
                new BpmnEvent("start", "startEvent"),
                new BpmnEvent("risk-end", "endEvent"),
                new BpmnEvent("default-end", "endEvent")
            ],
            Gateways: [new BpmnGateway("decision", "exclusiveGateway", "default")],
            Subprocesses: [],
            SequenceFlows:
            [
                new BpmnSequenceFlow("to-decision", "start", "decision"),
                new BpmnSequenceFlow(
                    "risk",
                    "decision",
                    "risk-end",
                    ConditionExpression: "= some risk in riskLevels satisfies risk = \"red\"",
                    Attributes: feelAttributes),
                new BpmnSequenceFlow("default", "decision", "default-end", IsDefault: true)
            ],
            Tasks: [],
            ProcessVariables: new Dictionary<string, object>
            {
                ["riskLevels"] = new[] { "green", "red" }
            });

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, entry => entry.Contains("ExclusiveFlowSelected: risk", StringComparison.Ordinal));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: risk-end", StringComparison.Ordinal));
        Assert.DoesNotContain(trace, entry => entry.Contains("EndEvent: default-end", StringComparison.Ordinal));
    }

    [Fact]
    public void Execution_Starts_Only_None_Start_Events_In_The_Selected_Process()
    {
        var primaryStart = new BpmnEvent("primary-start", "startEvent") { ProcessId = "primary" };
        var primaryEnd = new BpmnEvent("primary-end", "endEvent") { ProcessId = "primary" };
        var messageStart = new BpmnEvent(
            "message-start",
            "startEvent",
            [new MessageEventDefinition("message", null)]) { ProcessId = "primary" };
        var messageEnd = new BpmnEvent("message-end", "endEvent") { ProcessId = "primary" };
        var foreignStart = new BpmnEvent("foreign-start", "startEvent") { ProcessId = "foreign" };
        var foreignEnd = new BpmnEvent("foreign-end", "endEvent") { ProcessId = "foreign" };
        var model = new BpmnModel(
            "primary",
            "Scoped execution",
            Events: [primaryStart, primaryEnd, messageStart, messageEnd, foreignStart, foreignEnd],
            Gateways: [],
            Subprocesses: [],
            SequenceFlows:
            [
                new BpmnSequenceFlow("primary-flow", "primary-start", "primary-end") { ProcessId = "primary" },
                new BpmnSequenceFlow("message-flow", "message-start", "message-end") { ProcessId = "primary" },
                new BpmnSequenceFlow("foreign-flow", "foreign-start", "foreign-end") { ProcessId = "foreign" }
            ],
            Tasks: []);

        var trace = new ProcessEngine().Execute(model);

        Assert.Contains(trace, entry => entry.Contains("StartEvent: primary-start", StringComparison.Ordinal));
        Assert.Contains(trace, entry => entry.Contains("EndEvent: primary-end", StringComparison.Ordinal));
        Assert.DoesNotContain(trace, entry => entry.Contains("message-start", StringComparison.Ordinal));
        Assert.DoesNotContain(trace, entry => entry.Contains("foreign-start", StringComparison.Ordinal));
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
            new List<BpmnSequenceFlow> {
                new("flow1", "start1", "sub1"),
                new("flow2", "sub1", "end1")
            },
            new List<BpmnSubprocess> { new("sub1", true) }
        );
        var engine = new ProcessEngine();
        var trace = engine.Execute(model);
        Assert.Contains(trace, x => x.Contains("SubProcess: sub1"));
        //Assert.Contains(trace, x => x.Contains("MultiInstance: sub1"));
       // Assert.Contains(trace, x => x.Contains("SubprocessStart: sub1_start"));
       // Assert.Contains(trace, x => x.Contains("SubprocessEnd: sub1_end"));
    }

    private static BpmnModel CreateSimpleModel(string processId)
    {
        return new BpmnModel(
            processId,
            "Test",
            new List<BpmnEvent> { new("start1", "startEvent"), new("end1", "endEvent") },
            new List<BpmnTask>(),
            new List<BpmnGateway>(),
            new List<BpmnSequenceFlow> { new("flow1", "start1", "end1") },
            new List<BpmnSubprocess>());
    }

}
