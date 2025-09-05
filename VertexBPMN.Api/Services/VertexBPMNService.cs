namespace VertexBPMN.Api.Services;

using Grpc.Core;
using System.Threading.Tasks;
using VertexBPMN.Api.Configurations;
using VertexBPMN.Core.Domain;

public class VertexBPMNService : VertexBPMN.VertexBPMNService.VertexBPMNServiceBase
{
    private readonly IDistributedTokenEngine _engine;

    public VertexBPMNService(IDistributedTokenEngine engine)
    {
        _engine = engine;
    }

    public override async Task<CmmnResponse> RegisterCmmnModel(RegisterCmmnRequest request, ServerCallContext context)
    {
        await _engine.RegisterCmmnModelAsync(request.CaseId, request.CmmnXml);
        return new CmmnResponse { Message = $"CMMN model {request.CaseId} registered" };
    }

    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        var cmmnXml = await _engine.GetCmmnModelAsync(request.CaseId);
        var caseModel = await _engine.GetCmmnParser().ParseAsync(cmmnXml);
        var trace = await _engine.ExecuteCaseAsync(caseModel);
        return new ExecuteCaseResponse { Trace = { trace } };
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
        await _engine.TriggerUserEventAsync(request.CaseId, request.EventId, request.EventData.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value));
        return new CmmnResponse { Message = $"Event {request.EventId} triggered for case {request.CaseId}" };
    }

    public override async Task<CmmnResponse> UpdateCaseFileItem(CaseFileUpdateRequest request, ServerCallContext context)
    {
        await _engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue);
        return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" };
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        await _engine.GenerateAdHocSubprocessAsync(request.CaseId);
        return new CmmnResponse { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" };
    }
}
