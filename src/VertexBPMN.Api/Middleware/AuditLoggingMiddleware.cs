using System.Security.Claims;
using System.Text.Json;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Middleware;

public sealed class AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAuditLogService auditLogService)
    {
        await next(context);

        if (context.Request.Method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
            return;

        try
        {
            var correlationId = context.TraceIdentifier;
            var tenantId = context.User.FindFirstValue("tenant_id");
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? context.User.FindFirstValue(ClaimTypes.Name);
            await auditLogService.RecordAsync(new AuditLog
            {
                Action = $"HTTP_{context.Request.Method}",
                Resource = context.Request.Path,
                ResourceId = context.Request.RouteValues.Values.LastOrDefault()?.ToString(),
                UserId = userId,
                TenantId = tenantId,
                CorrelationId = correlationId,
                StatusCode = context.Response.StatusCode,
                DetailsJson = JsonSerializer.Serialize(new { context.Request.Method, context.Request.Path })
            }, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist audit record for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
    }
}