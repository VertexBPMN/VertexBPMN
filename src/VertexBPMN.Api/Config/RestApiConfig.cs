using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Config;


internal static class RestApiConfig
{
    public static WebApplication MapVertexBPMNApi(this WebApplication app, IProcessEngine engine)
    {
        app.MapPost("/api/cmmn/register", async (CaseRequest request) =>
        {
            await engine.RegisterCmmnModelAsync(request.CaseId, request.CmmnXml);
            return Results.Ok(new { Message = $"CMMN model {request.CaseId} registered" });
        });

        app.MapPost("/api/cmmn/execute", async (CaseRequest request, CancellationToken cancellationToken) =>
        {
            var caseModel = await engine.GetCmmnModelAsync(request.CaseId);
            var trace = await engine.ExecuteCaseAsync(caseModel, cancellationToken);
            return Results.Ok(new { Trace = trace });
        });

        app.MapPost("/api/cmmn/trigger-event", async (TriggerEventRequest request, CancellationToken cancellationToken) =>
        {
            await engine.TriggerUserEventAsync(request.CaseId, request.EventId, request.EventData, cancellationToken);
            return Results.Ok(new { Message = $"Event {request.EventId} triggered for case {request.CaseId}" });
        });

        app.MapPost("/api/cmmn/update-casefile", async (CaseFileUpdateRequest request, CancellationToken cancellationToken) =>
        {
            await engine.UpdateCaseFileItemAsync(request.CaseId, request.CaseFileItemId, request.NewValue, cancellationToken);
            return Results.Ok(new { Message = $"CaseFileItem {request.CaseFileItemId} updated for case {request.CaseId}" });
        });

        app.MapPost("/api/cmmn/generate-adhoc", async (GenerateAdHocSubprocessRequest request, CancellationToken cancellationToken) =>
        {
            await engine.GenerateAdHocSubprocessAsync(request.CaseId, cancellationToken);
            return Results.Ok(new { Message = $"Ad-hoc subprocess generated for case {request.CaseId}" });
        });

        return app;
    }
}
