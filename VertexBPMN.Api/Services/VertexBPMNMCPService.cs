namespace VertexBPMN.Api.Services;

using Grpc.Core;
using System.Threading.Tasks;
using VertexBPMN.Api.Configurations;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Engine;

public class VertexBPMNMCPService : VertexBPMN.MCP.VertexBPMNMCPService.VertexBPMNMCPServiceBase
{
    private readonly IDistributedTokenEngine _engine;
    private readonly ILogger<VertexBPMNMCPService> _logger;

    public VertexBPMNMCPService(IDistributedTokenEngine engine, ILogger<VertexBPMNMCPService> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        _logger.LogInformation("MCP: ExecuteCase called for case {CaseId}", request.CaseId);
        var cmmnXml = await _engine.GetCmmnModelAsync(request.CaseId);
        var caseModel = await _engine.GetCmmnParser().ParseAsync(cmmnXml);
        var trace = await _engine.ExecuteCaseAsync(caseModel);
        return new ExecuteCaseResponse { Trace = { trace } };
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
        _logger.LogInformation("MCP: TriggerUserEvent called for case {CaseId}, event {EventId}", request.CaseId, request.EventId);
        await _engine.TriggerUserEventAsync(request.CaseId, request.EventId, request.EventData.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value));
        return new CmmnResponse { Message = $"Event {request.EventId} triggered for case {request.CaseId}" };
    }

    public override async Task<CmmnResponse> UpdateCaseFileItem(CaseFileUpdateRequest request, ServerCallContext context)
    {
        _logger.LogInformation("MCP: UpdateCaseFileItem called for case {CaseId}, item {CaseFileItemId}", request.CaseId, request.CaseFileItemId);
        await _engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue);
        return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" };
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        _logger.LogInformation("MCP: GenerateAdHocSubprocess called for case {CaseId}", request.CaseId);
        await _engine.GenerateAdHocSubprocessAsync(request.CaseId);
        return new CmmnResponse { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" };
    }

    public override async Task<HistoricalContextResponse> GetHistoricalContext(HistoricalContextRequest request, ServerCallContext context)
    {
        _logger.LogInformation("MCP: GetHistoricalContext called for case {CaseId}", request.CaseId);
        var historicalData = await _engine.GetHistoricalCaseDataAsync(request.CaseId);
        var response = new HistoricalContextResponse();
        response.HistoricalData.AddRange(historicalData.Select(hd => new HistoricalCaseData
        {
            CaseId = hd.CaseId,
            CaseFile = { hd.CaseFile.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "") },
            CompletedPlanItems = { hd.CompletedPlanItems },
            Timestamp = hd.Timestamp.ToString("o")
        }));
        return response;
    }
}
