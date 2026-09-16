using VertexBPMN.Engine.Execution;
using Moq;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;
using VertexBPMN.Engine.Ecosystem;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Tests.Parsing;

public class ExternalTaskDefinitionTests(ITestOutputHelper output)
{
    private static string Model(string retries = "0", string deadline = "300", string owner = "serviceTask", string extra = "") => $$"""
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0" id="d">
          <process id="p"><startEvent id="s"/><{{owner}} id="review"><extensionElements>
            <vertex:externalTask topic="agent.contract-review" agentProfileRef="review.v1" maxRetries="{{retries}}" deadlineSeconds="{{deadline}}"/>
            {{extra}}
          </extensionElements></{{owner}}><endEvent id="e"/>
          <sequenceFlow id="f1" sourceRef="s" targetRef="review"/><sequenceFlow id="f2" sourceRef="review" targetRef="e"/></process>
        </definitions>
        """;

    [Fact]
    public async Task ZeroRetriesSurvivesRoundtripAndMeansOneAttempt()
    {
        var parser = new BpmnParser();
        var model = await parser.ParseAsync(Model(), TestContext.Current.CancellationToken);
        var definition = Assert.Single(model.Tasks).ExternalTask;
        Assert.NotNull(definition);
        Assert.Equal(1, definition.MaxAttempts);
        var reparsed = await parser.ParseAsync(new BpmnSerializer().Serialize(model), TestContext.Current.CancellationToken);
        Assert.Equal(definition, Assert.Single(reparsed.Tasks).ExternalTask);
    }

    [Theory]
    [InlineData("-1", "300")]
    [InlineData("10", "300")]
    [InlineData("2147483648", "300")]
    [InlineData("0", "0")]
    [InlineData("0", "86401")]
    public async Task InvalidLimitsAreRejected(string retries, string deadline)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BpmnParser().ParseAsync(Model(retries, deadline), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UserTaskCannotOwnExternalWork()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BpmnParser().ParseAsync(Model(owner: "userTask"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConnectorCannotBeCombinedWithExternalWork()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BpmnParser().ParseAsync(Model(extra: "<vertex:connector type='http'/>"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SimpleEngineRejectsBeforeGeneratingSimulatedResult()
    {
        var model = await new BpmnParser().ParseAsync(Model(), TestContext.Current.CancellationToken);
        var engine = new ProcessEngine();
        var error = Assert.Throws<InvalidOperationException>(() => engine.Execute(model));
        Assert.Equal("external_task_engine_unsupported", error.Message);
        Assert.Null(engine.LastExecutionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingPreservesExternalWorkAndMappings(bool nested)
    {
        var parser = new BpmnStreamingParser(new BpmnParserOptions { StreamingThreshold = 1 });
        var xml = Model(extra: "<vertex:ioMapping><vertex:input name='text' expression='document'/><vertex:output name='result' target='reviewResult'/></vertex:ioMapping>");
        if (nested) xml = xml.Replace("<serviceTask", "<subProcess id='scope'><serviceTask", StringComparison.Ordinal)
            .Replace("</serviceTask>", "</serviceTask></subProcess>", StringComparison.Ordinal);
        var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var task = Assert.Single(model.Tasks);
        Assert.NotNull(task.ExternalTask);
        Assert.Equal(nested ? "scope" : null, task.SubprocessId);
        Assert.Equal("document", task.Attributes!["vertex:ioMapping.input.text"]);
        Assert.Equal("reviewResult", task.Attributes["vertex:ioMapping.output.result"]);
        Assert.Equal(2, model.Events.Count);
        Assert.Equal(2, model.SequenceFlows.Count);
        var serialized = new BpmnSerializer().Serialize(model);
        output.WriteLine(serialized);
        var reparsed = await new BpmnParser().ParseAsync(serialized, TestContext.Current.CancellationToken);
        Assert.Equal(task.ExternalTask, Assert.Single(reparsed.Tasks).ExternalTask);
        Assert.Equal(task.SubprocessId, Assert.Single(reparsed.Tasks).SubprocessId);
        Assert.Equal("document", Assert.Single(reparsed.Tasks).Attributes!["vertex:ioMapping.input.text"]);
        Assert.Equal("reviewResult", Assert.Single(reparsed.Tasks).Attributes!["vertex:ioMapping.output.result"]);
    }

    [Fact]
    public async Task StrictRoundtripPreservesExternalDefinitionAndMappings()
    {
        var parser = new BpmnParser(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });
        var model = await parser.ParseAsync(Model(extra: "<vertex:ioMapping><vertex:input name='text' expression='document'/><vertex:output name='result' target='reviewResult'/></vertex:ioMapping>"), TestContext.Current.CancellationToken);
        var reloaded = await parser.ParseAsync(new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model), TestContext.Current.CancellationToken);
        var task = Assert.Single(reloaded.Tasks);
        Assert.Equal(Assert.Single(model.Tasks).ExternalTask, task.ExternalTask);
        Assert.Equal("document", task.Attributes!["vertex:ioMapping.input.text"]);
        Assert.Equal("reviewResult", task.Attributes["vertex:ioMapping.output.result"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiInstanceRoundtripRetainsTaskExecutionStructure(bool streaming)
    {
        var ct = TestContext.Current.CancellationToken;
        var xml = Model().Replace("</serviceTask>", "<multiInstanceLoopCharacteristics isSequential='true' xmlns:c='http://camunda.org/schema/1.0/bpmn' c:collection='documents' c:elementVariable='document'><completionCondition>done</completionCondition></multiInstanceLoopCharacteristics></serviceTask>", StringComparison.Ordinal);
        var parser = new BpmnParser();
        var model = streaming ? await new BpmnStreamingParser(new BpmnParserOptions { StreamingThreshold = 1 }).ParseAsync(xml, ct) : await parser.ParseAsync(xml, ct);
        var loop = Assert.IsType<MultiInstanceLoopCharacteristics>(Assert.Single(model.Tasks).Loop);
        Assert.True(loop.IsSequential);
        Assert.Equal("documents", loop.Collection);
        Assert.Equal("document", loop.ElementVariable);
        Assert.Equal("done", loop.CompletionCondition);
        var roundtrip = await parser.ParseAsync(new BpmnSerializer().Serialize(model), ct);
        Assert.Equal(loop, Assert.Single(roundtrip.Tasks).Loop);
        Assert.Equal(Assert.Single(model.Tasks).ExternalTask, Assert.Single(roundtrip.Tasks).ExternalTask);
    }

    [Fact]
    public async Task DuplicateDefinitionsAreRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BpmnParser().ParseAsync(
            Model(extra: "<vertex:externalTask topic='other' maxRetries='0' deadlineSeconds='1'/>"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SimpleEngineRejectsRegisteredCalledExternalModelBeforeExecution()
    {
        var parser = new BpmnParser();
        var child = await parser.ParseAsync(Model(), TestContext.Current.CancellationToken);
        var parent = await parser.ParseAsync("<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='parent'><startEvent id='s'/><callActivity id='call' calledElement='p'/><sequenceFlow id='f' sourceRef='s' targetRef='call'/></process></definitions>", TestContext.Current.CancellationToken);
        var engine = new ProcessEngine();
        engine.RegisterBpmnModel("p", child);
        Assert.Equal("external_task_engine_unsupported", Assert.Throws<InvalidOperationException>(() => engine.Execute(parent)).Message);
        Assert.Null(engine.LastExecutionId);
    }

    [Fact]
    public async Task WrongNamespaceIsRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new BpmnParser().ParseAsync(
            Model().Replace("https://vertexbpmn.io/schema/bpmn/1.0", "https://example.invalid/schema", StringComparison.Ordinal), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DistributedEngineRejectsCalledExternalModelBeforeDispatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var parser = new BpmnParser();
        var parent = await parser.ParseAsync("<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='parent'><startEvent id='s'/><callActivity id='call' calledElement='p'/><sequenceFlow id='f' sourceRef='s' targetRef='call'/></process></definitions>", ct);
        var store = new Moq.Mock<VertexBPMN.Domain.Interfaces.IProcessInstanceStore>();
        store.Setup(item => item.GetBpmnModelAsync("p")).ReturnsAsync(Model());
        var dispatcher = new Moq.Mock<VertexBPMN.Domain.Interfaces.IMessageDispatcher>(Moq.MockBehavior.Strict);
        using var engine = new DistributedProcessEngine(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributedProcessEngine>.Instance,
            Moq.Mock.Of<VertexBPMN.Domain.Interfaces.IServiceTaskRegistry>(), dispatcher.Object, store.Object,
            Moq.Mock.Of<VertexBPMN.Domain.Interfaces.IDmnEngine>(), Moq.Mock.Of<VertexBPMN.Domain.Interfaces.IDmnParser>(),
            Moq.Mock.Of<VertexBPMN.Domain.Interfaces.ICmmnParser>(), parser, Moq.Mock.Of<VertexBPMN.Domain.Interfaces.IAiDecisionService>(),
            OpenTelemetry.Trace.TracerProvider.Default);
        Assert.Equal("external_task_engine_unsupported", (await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ExecuteAsync(parent, ct))).Message);
        dispatcher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("<vertex:ioMapping unexpected='x'/>")]
    [InlineData("<vertex:ioMapping><vertex:input name='text' expression='document' secret='x'/></vertex:ioMapping>")]
    [InlineData("<vertex:ioMapping><vertex:output name='result' target='a.b'/></vertex:ioMapping>")]
    public async Task AmbiguousMappingsAreRejected(string mapping)
    {
        Assert.Equal("external_task_invalid_mapping", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new BpmnParser().ParseAsync(Model(extra: mapping), TestContext.Current.CancellationToken))).Message);
    }
}
