using Microsoft.AspNetCore.SignalR;

namespace VertexBPMN.Studio.Hubs;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "ASP.NET Core activates this public SignalR endpoint.")]
public sealed class ProcessMonitoringHub : Hub
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
