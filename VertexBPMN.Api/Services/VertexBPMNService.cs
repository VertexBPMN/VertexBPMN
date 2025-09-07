namespace VertexBPMN.Api.Services;

using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Api.Configurations;
using VertexBPMN.Core.Domain;

public class VertexBPMNService : VertexBPMN.VertexBPMNService.VertexBPMNServiceBase
{
    private readonly IDistributedTokenEngine _engine;
    private readonly ILogger<VertexBPMNService> _logger;

    public VertexBPMNService(IDistributedTokenEngine engine, ILogger<VertexBPMNService> logger)
    {
        _engine = engine ?? throw new System.ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    public override async Task<CmmnResponse> RegisterCmmnModel(RegisterCmmnRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        ValidateNotEmpty(request?.CmmnXml, nameof(request.CmmnXml));
        try
        {
            await _engine.RegisterCmmnModelAsync(request.CaseId, request.CmmnXml);
            _logger.LogInformation("Registered CMMN model {CaseId}", request.CaseId);
            return new CmmnResponse { Message = $"CMMN model {request.CaseId} registered" };
        }
        catch (System.Exception ex)
        {
            throw MapToRpcException(ex, "Failed to register CMMN model");
        }
    }

    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        try
        {
            // NOTE: Parsing is delegated here; ideally the engine should encapsulate this.
            // If engine provides helper methods (not part of interface snippet), they'll be used; otherwise this will compile if present.
            var cmmnXml = await _engine.GetCmmnModelAsync(request.CaseId); // assuming extension / concrete method exists
            var caseModel = await _engine.GetCmmnParser().ParseAsync(cmmnXml); // assuming extension / concrete method exists
            var trace = await _engine.ExecuteCaseAsync(caseModel, ct);
            return new ExecuteCaseResponse { Trace = { trace } };
        }
        catch (System.Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to execute case {request.CaseId}");
        }
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        ValidateNotEmpty(request?.EventId, nameof(request.EventId));
        try
        {
            var eventData = new Dictionary<string, object>(request.EventData.Count);
            foreach (var kvp in request.EventData)
            {
                eventData[kvp.Key] = kvp.Value; // keep as string; domain can coerce
            }
            await _engine.TriggerUserEventAsync(request.CaseId, request.EventId, eventData, ct);
            _logger.LogInformation("Triggered user event {EventId} for case {CaseId}", request.EventId, request.CaseId);
            return new CmmnResponse { Message = $"Event {request.EventId} triggered for case {request.CaseId}" };
        }
        catch (System.Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to trigger event {request.EventId} for case {request.CaseId}");
        }
    }

    public override async Task<CmmnResponse> UpdateCaseFileItem(CaseFileUpdateRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        ValidateNotEmpty(request?.CaseFileItemId, nameof(request.CaseFileItemId));
        try
        {
            await _engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue, ct);
            _logger.LogInformation("Updated case file item {ItemId} for case {CaseId}", request.CaseFileItemId, request.CaseId);
            return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" };
        }
        catch (System.Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to update case file item {request.CaseFileItemId} for case {request.CaseId}");
        }
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        ValidateNotEmpty(request?.CaseId, nameof(request.CaseId));
        try
        {
            await _engine.GenerateAdHocSubprocessAsync(request.CaseId, ct);
            _logger.LogInformation("Generated ad-hoc subprocess for case {CaseId}", request.CaseId);
            return new CmmnResponse { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" };
        }
        catch (System.Exception ex)
        {
            throw MapToRpcException(ex, $"Failed to generate ad-hoc subprocess for case {request.CaseId}");
        }
    }

    private static void ValidateNotEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be provided"));
    }

    private RpcException MapToRpcException(System.Exception ex, string message)
    {
        // Map known domain exceptions here (examples shown as comments)
        // if (ex is CaseNotFoundException) return new RpcException(new Status(StatusCode.NotFound, ex.Message));
        // if (ex is ValidationException) return new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));

        _logger.LogError(ex, message);
        return new RpcException(new Status(StatusCode.Internal, message));
    }
}
