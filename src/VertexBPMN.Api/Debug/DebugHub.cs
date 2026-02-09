using Microsoft.AspNetCore.SignalR;

namespace VertexBPMN.Api.Debug;

public class DebugHub : Hub
{
    public async Task JoinProcessGroup(string processInstanceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"process_{processInstanceId}");
    }

    public async Task LeaveProcessGroup(string processInstanceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"process_{processInstanceId}");
    }
}