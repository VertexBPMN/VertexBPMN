namespace VertexBPMN.Studio.Hubs;

public interface IProcessMonitoringClient
{
    Task ProcessInstanceStateChanged(string instanceId, string newState);
    Task TaskCreated(string taskId, string assignee);
    Task DeploymentCompleted(string deploymentId, string status);
    Task EngineStatusChanged(string engineId, bool isOnline);
}