namespace VertexBPMN.Studio.Hubs;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "SignalR generates client proxies for this public callback contract.")]
public interface IProcessMonitoringClient
{
    Task ProcessInstanceStateChanged(string instanceId, string newState);
    Task TaskCreated(string taskId, string assignee);
    Task DeploymentCompleted(string deploymentId, string status);
    Task EngineStatusChanged(string engineId, bool isOnline);
}
