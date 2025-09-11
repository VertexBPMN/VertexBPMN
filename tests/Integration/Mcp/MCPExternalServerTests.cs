using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Core.Contracts;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Messaging;
using VertexBPMN.Core.Modeling;
using VertexBPMN.EngineServices;

namespace VertexBPMN.Tests.Integration.Mcp;


public class MCPExternalServerTests
{
    private readonly Mock<ILogger<DistributedTokenEngine>> _loggerMock;
    private readonly Mock<IProcessInstanceStore> _storeMock;
    private readonly Mock<ICmmnParser> _cmmnParserMock;
    private readonly Mock<IAiDecisionService> _aiDecisionServiceMock;
    private readonly DistributedTokenEngine _engine;

    public MCPExternalServerTests()
    {
        _loggerMock = new Mock<ILogger<DistributedTokenEngine>>();
        _storeMock = new Mock<IProcessInstanceStore>();
        _aiDecisionServiceMock = new Mock<IAiDecisionService>();
        var dispatcherMock = new Mock<IMessageDispatcher>();
        _cmmnParserMock = new Mock<ICmmnParser>();
        var dmnEngineMock = new Mock<IDmnEngine>();
        var dmnParserMock = new Mock<IDmnParser>();
        var tracerProvider = new Mock<TracerProvider>().Object;
        var bpmnParserMock = new Mock<IBpmnParser>();

        _engine = new DistributedTokenEngine(
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
    public async Task ProcessCaseTokenAsync_ExecutesMcpAction_Successfully()
    {
        // Arrange
        var caseId = "case1";
        var planItemId = "adHoc1";
        var caseModel = new CaseModel(
            caseId,
            "Test Case",
            [
                new PlanItem(planItemId, "adHocSubprocess", "adHocSubprocessDef", new Dictionary<string, string>
                    {
                        { "mcpAction", "trigger_approval" },
                        { "mcpServerUrl", "http://cms-mcp:8080/api/mcp" }
                    })
            ],
            [],
            []
        );
        var caseToken = new CaseToken(Guid.NewGuid(), Guid.Parse(caseId), planItemId, "adHocSubprocess", new Dictionary<string, object>(), DateTime.UtcNow);
        var trace = new List<string>();

        _storeMock.Setup(s => s.GetCmmnModelAsync(caseId)).ReturnsAsync("<cmmn:case id='case1'>...</cmmn:case>");
        _cmmnParserMock.Setup(p => p.ParseAsync(It.IsAny<string>(), CancellationToken.None)).ReturnsAsync(caseModel);
        _storeMock.Setup(s => s.GetPendingCaseTokensAsync()).ReturnsAsync([caseToken]);
        _aiDecisionServiceMock.Setup(s => s.ExecuteMcpActionAsync(caseId, "http://cms-mcp:8080/api/mcp", "trigger_approval", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
          _engine.GetType().GetMethod("ProcessCaseTokenAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_engine, new object[] { caseToken, caseModel, trace, CancellationToken.None });

        // Assert
        _aiDecisionServiceMock.Verify(s => s.ExecuteMcpActionAsync(caseId, "http://cms-mcp:8080/api/mcp", "trigger_approval", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()), Times.Once());
        Assert.Contains($"MCPActionTriggered: trigger_approval on http://cms-mcp:8080/api/mcp", trace);
    }

    [Fact]
    public async Task PredictOptimalPlanItemsAsync_UsesExternalMcpContext_Successfully()
    {
        // Arrange
        var caseId = "case1";
        var caseFile = new Dictionary<string, object> { { "key", "value" } };
        var historicalData = new List<HistoricalCaseData>
            {
                new HistoricalCaseData(caseId, caseFile, ["planItem1"], DateTime.UtcNow)
            };
        var externalContext = new Dictionary<string, object> { { "externalKey", "externalValue" } };
        var predictedPlanItems = new List<PlanItem>
            {
                new PlanItem("predicted1", "humanTask", "humanTaskDef", new Dictionary<string, string> { { "camunda:assignee", "user1" } })
            };

        _aiDecisionServiceMock.Setup(s => s.FetchExternalContextAsync(caseId, "external_workflow_data", It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalContext);
        _aiDecisionServiceMock.Setup(s => s.PredictOptimalPlanItemsAsync(caseId, caseFile, historicalData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(predictedPlanItems);

        // Act
        var result = await _aiDecisionServiceMock.Object.PredictOptimalPlanItemsAsync(caseId, caseFile, historicalData);

        // Assert
        Assert.Equal(predictedPlanItems, result);
        _aiDecisionServiceMock.Verify(s => s.FetchExternalContextAsync(caseId, "external_workflow_data", It.IsAny<CancellationToken>()), Times.Once());
    }
}
