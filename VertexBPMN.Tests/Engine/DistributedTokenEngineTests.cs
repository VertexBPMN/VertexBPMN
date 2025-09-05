using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Messaging;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Tests.Engine;

public class DistributedTokenEngineTests
{
    private readonly Mock<ILogger<DistributedTokenEngine>> _loggerMock;
    private readonly Mock<IProcessInstanceStore> _storeMock;
    private readonly Mock<IMessageDispatcher> _dispatcherMock;
    private readonly Mock<ICmmnParser> _cmmnParserMock;
    private readonly Mock<IAiDecisionService> _aiDecisionServiceMock;

    private readonly DistributedTokenEngine _engine;

    public DistributedTokenEngineTests()
    {
        _loggerMock = new Mock<ILogger<DistributedTokenEngine>>();
        _storeMock = new Mock<IProcessInstanceStore>();
        _dispatcherMock = new Mock<IMessageDispatcher>();
        _cmmnParserMock = new Mock<ICmmnParser>();
        _aiDecisionServiceMock = new Mock<IAiDecisionService>();
        var tracerProvider = new Mock<TracerProvider>().Object;
        var registry = new ServiceTaskRegistry();
        var dmnParser = new Mock<IDmnParser>();
        var bpmnParser = new Mock<IBpmnParser>();
        var dmnEngine = new Mock<IDmnEngine>();
        _engine = new DistributedTokenEngine(_loggerMock.Object, registry, _dispatcherMock.Object, _storeMock.Object, dmnEngine.Object, dmnParser.Object, _cmmnParserMock.Object, bpmnParser.Object, _aiDecisionServiceMock.Object, tracerProvider);

    }

    [Fact]
    public async Task TriggerUserEventAsync_TriggersEventListener_Successfully()
    {
        // Arrange
        var caseId = "case1";
        var eventId = "event1";
        var caseModel = new CaseModel(
            caseId,
            "Test Case",
            [
                new PlanItem(eventId, "eventListener", "userEventListener", null, null)
            ],
            [],
            []
        );
        var caseToken = new CaseToken(Guid.NewGuid(), Guid.Parse(caseId), eventId, "eventListener", new Dictionary<string, object>(), DateTime.UtcNow);

        _storeMock.Setup(s => s.GetCmmnModelAsync(caseId)).ReturnsAsync("<cmmn:case id='case1'>...</cmmn:case>");
        _cmmnParserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(caseModel);
        _storeMock.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([caseToken]);
        _dispatcherMock.Setup(d => d.PublishCaseTokenAsync(It.IsAny<CaseToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _engine.TriggerUserEventAsync(caseId, eventId, new Dictionary<string, object> { { "key", "value" } }, CancellationToken.None);

        // Assert
        _dispatcherMock.Verify(d => d.PublishCaseTokenAsync(It.Is<CaseToken>(t => t.CurrentPlanItemId == eventId), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task PublishCaseFileUpdateAsync_KafkaIntegration_Successfully()
    {
        // Arrange
        var caseId = "case1";
        var caseFileItemId = "amount";
        var newValue = 300;
        var caseModel = new CaseModel(
            caseId,
            "Test Case",
            [
                new PlanItem("event1", "eventListener", "caseFileItemUpdate", null, null)
            ],
            [],
            [
                new CaseFileItem(caseFileItemId, "Amount", 200)
            ]
        );
        var caseToken = new CaseToken(Guid.NewGuid(), Guid.Parse(caseId), "event1", "eventListener", new Dictionary<string, object> { { caseFileItemId, 200 } }, DateTime.UtcNow);

        _storeMock.Setup(s => s.GetCmmnModelAsync(caseId)).ReturnsAsync("<cmmn:case id='case1'>...</cmmn:case>");
        _cmmnParserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(caseModel);
        _storeMock.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([caseToken]);
        _storeMock.Setup(s => s.UpdateCaseModelAsync(It.IsAny<CaseModel>())).Returns(Task.CompletedTask);
        _dispatcherMock.Setup(d => d.PublishCaseFileUpdateAsync(It.IsAny<CaseFileUpdateEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _dispatcherMock.Setup(d => d.PublishCaseTokenAsync(It.IsAny<CaseToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _engine.UpdateCaseFileItemAsync(caseId, caseFileItemId, newValue, CancellationToken.None);

        // Assert
        _dispatcherMock.Verify(d => d.PublishCaseFileUpdateAsync(It.Is<CaseFileUpdateEvent>(e => e.CaseId == caseId && e.CaseFileItemId == caseFileItemId && e.NewValue.Equals(newValue)), It.IsAny<CancellationToken>()), Times.Once());
        _dispatcherMock.Verify(d => d.PublishCaseTokenAsync(It.Is<CaseToken>(t => t.CaseFile[caseFileItemId].Equals(newValue)), It.IsAny<CancellationToken>()), Times.Once());
    }
}
