using System.Security.Claims;
using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using VertexBPMN.Api.Grpc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Mcp;

[Authorize]
public sealed class VertexBpmnServiceImpl(
    ICaseExecutionRuntime cases,
    ILogger<VertexBpmnServiceImpl> logger) : VertexBPMNService.VertexBPMNServiceBase
{
    public override async Task<CmmnResponse> RegisterCmmnModel(RegisterCmmnRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.CmmnXml, nameof(request.CmmnXml));
        try
        {
            await cases.DeployAsync(request.CaseId, request.CaseId, request.CmmnXml, Tenant(context), context.CancellationToken);
            return new CmmnResponse { Message = $"CMMN model {request.CaseId} registered" };
        }
        catch (Exception exception)
        {
            throw Map(exception, logger);
        }
    }

    public override async Task<ExecuteCaseResponse> ExecuteCase(ExecuteCaseRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        try
        {
            var result = await cases.StartAsync(request.CaseId, Tenant(context), cancellationToken: context.CancellationToken);
            var response = new ExecuteCaseResponse { CaseInstanceId = result.Instance.Id.ToString() };
            response.Trace.AddRange(result.Trace);
            return response;
        }
        catch (Exception exception)
        {
            throw Map(exception, logger);
        }
    }

    public override async Task<CmmnResponse> TriggerUserEvent(TriggerEventRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.EventId, nameof(request.EventId));
        try
        {
            var instance = await ResolveActiveAsync(request.CaseId, context);
            var data = request.EventData.ToDictionary(item => item.Key, item => (object)item.Value);
            await cases.TriggerUserEventAsync(instance.Id, request.EventId, data, Tenant(context), context.CancellationToken);
            return new CmmnResponse { Message = $"Event {request.EventId} triggered for case {instance.Id}" };
        }
        catch (Exception exception)
        {
            throw Map(exception, logger);
        }
    }

    public override async Task<CmmnResponse> UpdateCaseFileItem(CaseFileUpdateRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        Validate(request.CaseFileItemId, nameof(request.CaseFileItemId));
        try
        {
            var instance = await ResolveActiveAsync(request.CaseId, context);
            await cases.UpdateCaseFileItemAsync(instance.Id, request.CaseFileItemId, request.NewValue, Tenant(context), context.CancellationToken);
            return new CmmnResponse { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {instance.Id}" };
        }
        catch (Exception exception)
        {
            throw Map(exception, logger);
        }
    }

    public override async Task<CmmnResponse> GenerateAdHocSubprocess(GenerateAdHocSubprocessRequest request, ServerCallContext context)
    {
        Validate(request.CaseId, nameof(request.CaseId));
        try
        {
            var instance = await ResolveActiveAsync(request.CaseId, context);
            var planItemId = string.IsNullOrWhiteSpace(request.PlanItemId)
                ? FirstDiscretionaryItem(instance)
                : request.PlanItemId;
            await cases.ActivateDiscretionaryItemAsync(instance.Id, planItemId, Tenant(context), context.CancellationToken);
            return new CmmnResponse { Message = $"Discretionary item {planItemId} activated for case {instance.Id}" };
        }
        catch (Exception exception)
        {
            throw Map(exception, logger);
        }
    }

    private async Task<CaseInstanceRecord> ResolveActiveAsync(string identifier, ServerCallContext context) =>
        await cases.ResolveInstanceAsync(identifier, Tenant(context), context.CancellationToken)
        ?? throw new KeyNotFoundException($"Active case '{identifier}' was not found.");

    internal static string Tenant(ServerCallContext context) =>
        context.GetHttpContext().User.FindFirstValue("tenant_id") ?? "default";

    internal static string FirstDiscretionaryItem(CaseInstanceRecord instance)
    {
        var states = JsonSerializer.Deserialize<Dictionary<string, string>>(instance.PlanItemStatesJson) ?? [];
        return states.FirstOrDefault(item => item.Value == "Discretionary").Key
            ?? throw new InvalidOperationException("The case has no available discretionary item.");
    }

    internal static void Validate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} is required"));
    }

    internal static RpcException Map(Exception exception, ILogger logger)
    {
        if (exception is RpcException rpc) return rpc;
        logger.LogWarning(exception, "CMMN gRPC operation failed");
        return exception switch
        {
            KeyNotFoundException => new RpcException(new Status(StatusCode.NotFound, exception.Message)),
            InvalidOperationException => new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message)),
            ArgumentException => new RpcException(new Status(StatusCode.InvalidArgument, exception.Message)),
            _ => new RpcException(new Status(StatusCode.Internal, "CMMN operation failed."))
        };
    }
}
