using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Features;
using VertexBPMN.Api.Grpc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Mcp;

[Authorize]
public class VertexBpmnServiceImpl : VertexBPMNService.VertexBPMNServiceBase
{
    private readonly IProcessEngine _engine;
    private readonly ILogger<VertexBpmnServiceImpl> _logger;
    private readonly AdvancedFeatureOptions _features;

    public VertexBpmnServiceImpl(IProcessEngine engine,
                                 ILogger<VertexBpmnServiceImpl> logger,
                                 IOptions<AdvancedFeatureOptions> features)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _features = features?.Value ?? throw new ArgumentNullException(nameof(features));
    }

    public override async Task<CmmnResponse> RegisterCmmnModel(RegisterCmmnRequest request, ServerCallContext context)
    {
        EnsureCmmnExecutionEnabled();
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.CmmnXml, nameof(request.CmmnXml));
        await _engine.RegisterCmmnModelAsync(request.CaseId, request.CmmnXml);
        return new CmmnResponse { Message = $"CMMN model {request.CaseId} registered" };
    }

    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        EnsureCmmnExecutionEnabled();
        Validate(request.CaseId, nameof(request.CaseId));
        var model = await _engine.GetCmmnModelAsync(request.CaseId);
        var trace = await _engine.ExecuteCaseAsync(model, context.CancellationToken);
        var resp = new ExecuteCaseResponse();
        resp.Trace.AddRange(trace);
        return resp;
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
        EnsureCmmnExecutionEnabled();
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.EventId, nameof(request.EventId));

        var data = new Dictionary<string, object>(request.EventData.Count);
        foreach (var kv in request.EventData)
            data[kv.Key] = kv.Value;

        await _engine.TriggerUserEventAsync(request.CaseId, request.EventId, data, context.CancellationToken);
        return new CmmnResponse { Message = $"Event {request.EventId} triggered for case {request.CaseId}" };
    }

    public override async Task<CmmnResponse> UpdateCaseFileItem(CaseFileUpdateRequest request, ServerCallContext context)
    {
        EnsureCmmnExecutionEnabled();
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.CaseFileItemId, nameof(request.CaseFileItemId));

        await _engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue, context.CancellationToken);
        return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" };
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        EnsureCmmnExecutionEnabled();
        Validate(request.CaseId, nameof(request.CaseId));
        await _engine.GenerateAdHocSubprocessAsync(request.CaseId, context.CancellationToken);
        return new CmmnResponse { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" };
    }

    private static void Validate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} is required"));
    }

    private void EnsureCmmnExecutionEnabled()
    {
        if (!_features.CmmnExecution)
            throw new RpcException(new Status(
                StatusCode.Unimplemented,
                "CMMN execution is not qualified and is disabled."));
    }
}
