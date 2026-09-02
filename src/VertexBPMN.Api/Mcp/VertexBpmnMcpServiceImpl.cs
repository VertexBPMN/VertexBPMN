using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using VertexBPMN.Api.Grpc.Mcp;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Mcp;

[Authorize]
public sealed class VertexBpmnMcpServiceImpl(
    ICaseExecutionRuntime cases,
    ILogger<VertexBpmnMcpServiceImpl> logger) : VertexBPMNMCPService.VertexBPMNMCPServiceBase
{
    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        VertexBpmnServiceImpl.Validate(request.CaseId, nameof(request.CaseId));
        try
        {
            var result = await cases.StartAsync(request.CaseId, Tenant(context), cancellationToken: context.CancellationToken);
            var response = new ExecuteCaseResponse { CaseInstanceId = result.Instance.Id.ToString() };
            response.Trace.AddRange(result.Trace);
            return response;
        }
        catch (Exception exception)
        {
            throw VertexBpmnServiceImpl.Map(exception, logger);
        }
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
        VertexBpmnServiceImpl.Validate(request.CaseId, nameof(request.CaseId));
        VertexBpmnServiceImpl.Validate(request.EventId, nameof(request.EventId));
        try
        {
            var instance = await ResolveActiveAsync(request.CaseId, context);
            var data = request.EventData.ToDictionary(item => item.Key, item => (object)item.Value);
            await cases.TriggerUserEventAsync(instance.Id, request.EventId, data, Tenant(context), context.CancellationToken);
            return new CmmnResponse { Message = $"Event {request.EventId} triggered for case {instance.Id}" };
        }
        catch (Exception exception)
        {
            throw VertexBpmnServiceImpl.Map(exception, logger);
        }
    }

    public override async Task<CmmnResponse> UpdateCaseFileItem(CaseFileUpdateRequest request, ServerCallContext context)
    {
        VertexBpmnServiceImpl.Validate(request.CaseId, nameof(request.CaseId));
        VertexBpmnServiceImpl.Validate(request.CaseFileItemId, nameof(request.CaseFileItemId));
        try
        {
            var instance = await ResolveActiveAsync(request.CaseId, context);
            await cases.UpdateCaseFileItemAsync(instance.Id, request.CaseFileItemId, request.NewValue, Tenant(context), context.CancellationToken);
            return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {instance.Id}" };
        }
        catch (Exception exception)
        {
            throw VertexBpmnServiceImpl.Map(exception, logger);
        }
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        VertexBpmnServiceImpl.Validate(request.CaseId, nameof(request.CaseId));
        try
        {
            var instance = await ResolveActiveAsync(request.CaseId, context);
            var planItemId = string.IsNullOrWhiteSpace(request.PlanItemId)
                ? VertexBpmnServiceImpl.FirstDiscretionaryItem(instance)
                : request.PlanItemId;
            await cases.ActivateDiscretionaryItemAsync(instance.Id, planItemId, Tenant(context), context.CancellationToken);
            return new CmmnResponse { Message = $"Discretionary item {planItemId} activated for case {instance.Id}" };
        }
        catch (Exception exception)
        {
            throw VertexBpmnServiceImpl.Map(exception, logger);
        }
    }

    public override async Task<HistoricalContextResponse> GetHistoricalContext(HistoricalContextRequest request, ServerCallContext context)
    {
        VertexBpmnServiceImpl.Validate(request.CaseId, nameof(request.CaseId));
        try
        {
            var instance = await cases.ResolveInstanceAsync(request.CaseId, Tenant(context), context.CancellationToken)
                ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
            var history = await cases.GetHistoryAsync(instance.Id, Tenant(context), context.CancellationToken);
            var response = new HistoricalContextResponse();
            foreach (var entry in history)
            {
                var item = new HistoricalCaseData
                {
                    CaseId = entry.CaseInstanceId.ToString(),
                    Timestamp = entry.Timestamp.ToString("O")
                };
                foreach (var value in entry.CaseFile)
                    item.CaseFile[value.Key] = value.Value?.ToString() ?? string.Empty;
                item.CompletedPlanItems.AddRange(entry.CompletedPlanItems);
                response.HistoricalData.Add(item);
            }
            return response;
        }
        catch (Exception exception)
        {
            throw VertexBpmnServiceImpl.Map(exception, logger);
        }
    }

    private async Task<CaseInstanceRecord> ResolveActiveAsync(string identifier, ServerCallContext context) =>
        await cases.ResolveInstanceAsync(identifier, Tenant(context), context.CancellationToken)
        ?? throw new KeyNotFoundException($"Active case '{identifier}' was not found.");

    private static string Tenant(ServerCallContext context) => VertexBpmnServiceImpl.Tenant(context);
}
