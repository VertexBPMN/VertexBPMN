using Microsoft.AspNetCore.SignalR;

namespace VertexBPMN.Studio.Hubs;

public class ProcessMonitoringHub : Hub
{
    public async Task JoinEngineGroup(string engineId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"engine-{engineId}");
    }

    public async Task LeaveEngineGroup(string engineId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"engine-{engineId}");
    }
}