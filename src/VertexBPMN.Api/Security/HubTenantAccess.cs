using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Security;

internal static class HubTenantAccess
{
    public static void RequireTenant(HubCallerContext context, string? tenant)
    {
        if (context.User?.IsInRole("Admin") == true) return;
        var own = context.User?.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(own) || !string.Equals(own, tenant, StringComparison.Ordinal))
            throw new HubException("Access denied.");
    }

    public static async Task<Guid> RequireProcessAsync(HubCallerContext context, IRuntimeService runtime, string id)
    {
        if (!Guid.TryParse(id, out var processId)) throw new HubException("Access denied.");
        var instance = await runtime.GetByIdAsync(processId, context.ConnectionAborted);
        if (instance is null) throw new HubException("Access denied.");
        RequireTenant(context, instance.TenantId);
        return processId;
    }
}
