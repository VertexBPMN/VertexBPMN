using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using VertexBPMN.Api.Security;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Debug;

[Authorize(Policy = "TenantReadOnly")]
public class DebugHub(IRuntimeService runtime) : Hub
{
    public async Task JoinProcessGroup(string processInstanceId)
    {
        var id = await HubTenantAccess.RequireProcessAsync(Context, runtime, processInstanceId);
        processInstanceId = id.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"process_{processInstanceId}");
    }

    public async Task LeaveProcessGroup(string processInstanceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"process_{processInstanceId}");
    }
}
