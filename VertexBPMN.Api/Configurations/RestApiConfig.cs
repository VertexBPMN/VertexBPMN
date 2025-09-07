using VertexBPMN.Core.Domain;

namespace VertexBPMN.Api.Configurations;

public record CaseRequest(string CaseId, string CmmnXml);
public record TriggerEventRequest(string CaseId, string EventId, Dictionary<string, object> EventData);
public record CaseFileUpdateRequest(string CaseId, string CaseFileItemId, object NewValue);
public record GenerateAdHocSubprocessRequest(string CaseId);


public static class RestApiConfig
{
    public static WebApplication MapVertexBPMNApi(this WebApplication app, IDistributedTokenEngine engine)
    {
        app.MapPost("/api/cmmn/register", async (HttpContext context, CaseRequest request) =>
        {
            await engine.RegisterCmmnModelAsync(request.CaseId, request.CmmnXml);
            return Results.Ok(new { Message = $"CMMN model {request.CaseId} registered" });
        });

        app.MapPost("/api/cmmn/execute", async (HttpContext context, CaseRequest request) =>
        {
            var caseModel = await engine.GetCmmnModelAsync(request.CaseId);
            var trace = await engine.ExecuteCaseAsync(caseModel);
            return Results.Ok(new { Trace = trace });
        });

        app.MapPost("/api/cmmn/trigger-event", async (HttpContext context, TriggerEventRequest request) =>
        {
            await engine.TriggerUserEventAsync(request.CaseId, request.EventId, request.EventData);
            return Results.Ok(new { Message = $"Event {request.EventId} triggered for case {request.CaseId}" });
        });

        app.MapPost("/api/cmmn/update-casefile", async (HttpContext context, CaseFileUpdateRequest request) =>
        {
            await engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue);
            return Results.Ok(new { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" });
        });

        app.MapPost("/api/cmmn/generate-adhoc", async (HttpContext context, GenerateAdHocSubprocessRequest request) =>
        {
            await engine.GenerateAdHocSubprocessAsync(request.CaseId);
            return Results.Ok(new { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" });
        });

        return app;
    }
}
