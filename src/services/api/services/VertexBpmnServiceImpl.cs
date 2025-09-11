using Grpc.Core;
using VertexBPMN.Api.Grpc;
using VertexBPMN.Core.Contracts;

namespace VertexBPMN.Api.Services;

public class VertexBpmnServiceImpl : VertexBPMNService.VertexBPMNServiceBase
{
    private readonly IDistributedTokenEngine _engine;
    private readonly ILogger<VertexBpmnServiceImpl> _logger;

    public VertexBpmnServiceImpl(IDistributedTokenEngine engine,
                                 ILogger<VertexBpmnServiceImpl> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<CmmnResponse> RegisterCmmnModel(RegisterCmmnRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.CmmnXml, nameof(request.CmmnXml));
        await _engine.RegisterCmmnModelAsync(request.CaseId, request.CmmnXml);
        return new CmmnResponse { Message = $"CMMN model {request.CaseId} registered" };
    }

    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        var model = await _engine.GetCmmnModelAsync(request.CaseId);
        var trace = await _engine.ExecuteCaseAsync(model, context.CancellationToken);
        var resp = new ExecuteCaseResponse();
        resp.Trace.AddRange(trace);
        return resp;
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
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
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.CaseFileItemId, nameof(request.CaseFileItemId));

        await _engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue, context.CancellationToken);
        return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" };
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        await _engine.GenerateAdHocSubprocessAsync(request.CaseId, context.CancellationToken);
        return new CmmnResponse { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" };
    }

    private static void Validate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} is required"));
    }
}