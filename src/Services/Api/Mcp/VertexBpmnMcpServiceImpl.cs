using Grpc.Core;
using VertexBPMN.Api.Grpc.Mcp;
using VertexBPMN.Core.Contracts;
using HistoricalCaseData = VertexBPMN.Api.Grpc.Mcp.HistoricalCaseData;

namespace VertexBPMN.Api.Mcp;

public class VertexBpmnMcpServiceImpl : VertexBPMNMCPService.VertexBPMNMCPServiceBase
{
    private readonly IDistributedTokenEngine _engine;
    private readonly ILogger<VertexBpmnMcpServiceImpl> _logger;

    public VertexBpmnMcpServiceImpl(
        IDistributedTokenEngine engine,
        ILogger<VertexBpmnMcpServiceImpl> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        try
        {
            _logger.LogInformation("MCP ExecuteCase: {CaseId}", request.CaseId);

            var model = await _engine.GetCmmnModelAsync(request.CaseId);
            var trace = await _engine.ExecuteCaseAsync(model, context.CancellationToken);

            var response = new ExecuteCaseResponse();
            response.Trace.AddRange(trace);
            return response;
        }
        catch (Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to execute case {request.CaseId}");
        }
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        ValidateNotEmpty(request?.EventId, nameof(request.EventId));
        try
        {
            _logger.LogInformation("MCP TriggerUserEvent: Case={CaseId}, Event={EventId}", request.CaseId, request.EventId);

            var payload = new Dictionary<string, object>(request.EventData.Count);
            foreach (var kv in request.EventData)
                payload[kv.Key] = kv.Value;

            await _engine.TriggerUserEventAsync(request.CaseId, request.EventId, payload, context.CancellationToken);
            return new Grpc.Mcp.CmmnResponse { Message = $"Event {request.EventId} triggered for case {request.CaseId}" };
        }
        catch (Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to trigger event {request.EventId} for case {request.CaseId}");
        }
    }

    public override async Task<CmmnResponse> UpdateCaseFileItem(CaseFileUpdateRequest request, ServerCallContext context)
    {
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        ValidateNotEmpty(request?.CaseFileItemId, nameof(request.CaseFileItemId));
        try
        {
            _logger.LogInformation("MCP UpdateCaseFileItem: Case={CaseId}, Item={ItemId}", request.CaseId, request.CaseFileItemId);

            await _engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue, context.CancellationToken);
            return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" };
        }
        catch (Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to update case file item {request.CaseFileItemId} for case {request.CaseId}");
        }
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        try
        {
            _logger.LogInformation("MCP GenerateAdHocSubprocess: {CaseId}", request.CaseId);

            await _engine.GenerateAdHocSubprocessAsync(request.CaseId, context.CancellationToken);
            return new CmmnResponse { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" };
        }
        catch (Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to generate ad-hoc subprocess for case {request.CaseId}");
        }
    }

    public override async Task<HistoricalContextResponse> GetHistoricalContext(HistoricalContextRequest request, ServerCallContext context)
    {
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        try
        {
            _logger.LogInformation("MCP GetHistoricalContext: {CaseId}", request.CaseId);

            var history = await _engine.GetHistoricalCaseDataAsync(request.CaseId);
            var response = new HistoricalContextResponse();

            foreach (var h in history)
            {
                var dto = new HistoricalCaseData
                {
                    CaseId = h.CaseId,
                    Timestamp = h.Timestamp.ToString("o")
                };
                foreach (var kv in h.CaseFile)
                {
                    dto.CaseFile[kv.Key] = kv.Value?.ToString() ?? string.Empty;
                }
                dto.CompletedPlanItems.AddRange(h.CompletedPlanItems);
                response.HistoricalData.Add(dto);
            }
            return response;
        }
        catch (Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to load historical context for case {request.CaseId}");
        }
    }

    private static void ValidateNotEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be provided"));
    }

    private RpcException MapToRpcException(Exception ex, string message)
    {
        // Domain-spezifische Exception-Mappings (Platzhalter):
        // if (ex is CaseNotFoundException) return new RpcException(new Status(StatusCode.NotFound, ex.Message));
        // if (ex is ValidationException)  return new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));

        _logger.LogError(ex, message);
        return new RpcException(new Status(StatusCode.Internal, message));
    }
}