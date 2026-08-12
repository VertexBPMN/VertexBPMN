using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Application.Messaging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Infrastructure.Persistence.InMemory;

namespace VertexBPMN.Tests.Conformance;

public sealed class DistributedConformanceTests
{
    public static IEnumerable<object[]> ReferenceModels()
    {
        var referenceDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference");

        return Directory
            .EnumerateFiles(referenceDirectory, "*.bpmn")
            .Where(path => !Path.GetFileName(path).Equals("C.7.0.bpmn", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new object[] { path });
    }

    [Theory]
    [MemberData(nameof(ReferenceModels))]
    public async Task DistributedEngine_ExecutesReferenceModel(string bpmnFile)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        cancellationToken = timeoutCts.Token;
        var xml = await File.ReadAllTextAsync(bpmnFile, cancellationToken);
        var parserLogger = new Mock<ILogger<BpmnParser>>();
        var parser = new BpmnParser(parserLogger.Object, TracerProvider.Default);
        var model = await parser.ParseAsync(xml.Replace('\'', '"'), cancellationToken);

        var registry = new ServiceTaskRegistry();
        var dispatcher = new Mock<IMessageDispatcher>();
        dispatcher
            .Setup(d => d.PublishTokenAsync(It.IsAny<VertexBPMN.Domain.Entities.ExecutionToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var store = new InMemoryProcessInstanceStore();
        var dmnParser = new Mock<IDmnParser>();
        var cmmnParser = new Mock<ICmmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        var aiDecisionService = new Mock<IAiDecisionService>();
        using var engine = new DistributedProcessEngine(
            new LoggerFactory().CreateLogger<DistributedProcessEngine>(),
            registry,
            dispatcher.Object,
            store,
            dmnEngine.Object,
            dmnParser.Object,
            cmmnParser.Object,
            bpmnParser.Object,
            aiDecisionService.Object,
            TracerProvider.Default);

        var trace = await engine.ExecuteAsync(model, cancellationToken);

        Assert.NotEmpty(trace);
        Assert.Contains(trace, entry => entry.StartsWith("DistributedExecution: Starting process", StringComparison.Ordinal));
    }
}