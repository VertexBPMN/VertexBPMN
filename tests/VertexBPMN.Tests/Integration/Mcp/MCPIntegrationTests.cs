using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Application;

using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Engine.Execution;

namespace VertexBPMN.Tests.Integration.Mcp;


public class MCPIntegrationTests
{
    private readonly Mock<ILogger<DistributedProcessEngine>> _loggerMock;
    private readonly Mock<IProcessInstanceStore> _storeMock;
    private readonly Mock<ICmmnParser> _cmmnParserMock;
    private readonly Mock<IAiDecisionService> _aiDecisionServiceMock;
    private readonly DistributedProcessEngine _engine;

    public MCPIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<DistributedProcessEngine>>();
        _storeMock = new Mock<IProcessInstanceStore>();
        _aiDecisionServiceMock = new Mock<IAiDecisionService>();
        var dispatcherMock = new Mock<IMessageDispatcher>();
        _cmmnParserMock = new Mock<ICmmnParser>();
        var dmnEngineMock = new Mock<IDmnEngine>();
        var dmnParserMock = new Mock<IDmnParser>();
        var bpmnParserMock = new Mock<IBpmnParser>();

        var tracerProvider = new Mock<TracerProvider>().Object;

        _engine = new DistributedProcessEngine(
            _loggerMock.Object,
            new ServiceTaskRegistry(),
            dispatcherMock.Object,
            _storeMock.Object,
            dmnEngineMock.Object,
            dmnParserMock.Object,
            _cmmnParserMock.Object,
            bpmnParserMock.Object,
            _aiDecisionServiceMock.Object,
            tracerProvider
        );
    }

    [Fact]
    public async Task GenerateAdHocSubprocessAsync_WithMCPContext_Successfully()
    {
        // Arrange
        var caseId = Guid.NewGuid().ToString();
        var caseModel = new CaseModel(caseId, "Test Case", [], [], []);
        var caseToken = new CaseToken(Guid.NewGuid(), Guid.Parse(caseId), "planItem1", "humanTask", new Dictionary<string, object>(), DateTime.UtcNow);
        var historicalData = new List<HistoricalCaseData>
            {
                new HistoricalCaseData(caseId, new Dictionary<string, object> { { "key", "value" } }, ["planItem1"], DateTime.UtcNow)
            };
        var predictedPlanItems = new List<PlanItem>
            {
                new PlanItem("predicted1", "humanTask", "humanTaskDef", new Dictionary<string, string> { { "camunda:assignee", "user1" } })
            };

        _storeMock.Setup(s => s.GetCmmnModelAsync(caseId)).ReturnsAsync("<cmmn:case id='case1'>...</cmmn:case>");
        _cmmnParserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(caseModel);
        _storeMock.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([caseToken]);
        _storeMock.Setup(s => s.GetHistoricalCaseDataAsync(caseId)).ReturnsAsync(historicalData);
        _aiDecisionServiceMock.Setup(s => s.PredictOptimalPlanItemsAsync(caseId, It.IsAny<Dictionary<string, object>>(), historicalData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(predictedPlanItems);
        _storeMock.Setup(s => s.SaveHistoricalCaseDataAsync(It.IsAny<HistoricalCaseData>())).Returns(Task.CompletedTask);
        _storeMock.Setup(s => s.UpdateCaseModelAsync(It.IsAny<CaseModel>())).Returns(Task.CompletedTask);

        // Act
        await _engine.GenerateAdHocSubprocessAsync(caseId);

        // Assert
        _aiDecisionServiceMock.Verify(s => s.PredictOptimalPlanItemsAsync(caseId, It.IsAny<Dictionary<string, object>>(), historicalData, It.IsAny<CancellationToken>()), Times.Once());
        _storeMock.Verify(s => s.SaveHistoricalCaseDataAsync(It.IsAny<HistoricalCaseData>()), Times.Once());
    }

    [Fact]
    public async Task FetchExternalContextAsync_MCPClient_Successfully()
    {
        // Arrange
        var caseId = "case1";
        var resourceId = "external_workflow_data";
        var externalContext = new Dictionary<string, object> { { "externalKey", "externalValue" } };

        _aiDecisionServiceMock.Setup(s => s.FetchExternalContextAsync(caseId, resourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalContext);

        // Act
        var result = await _aiDecisionServiceMock.Object.FetchExternalContextAsync(caseId, resourceId);

        // Assert
        Assert.Equal(externalContext, result);
        _aiDecisionServiceMock.Verify(s => s.FetchExternalContextAsync(caseId, resourceId, It.IsAny<CancellationToken>()), Times.Once());
    }
}
