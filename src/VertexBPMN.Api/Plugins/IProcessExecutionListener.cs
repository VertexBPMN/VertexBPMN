namespace VertexBPMN.Api.Plugins;

public interface IProcessExecutionListener
{
    Task OnProcessStartedAsync(Guid processInstanceId, Dictionary<string, object> variables);
    Task OnProcessCompletedAsync(Guid processInstanceId, Dictionary<string, object> variables);
    Task OnActivityStartedAsync(Guid processInstanceId, string activityId, Dictionary<string, object> variables);
    Task OnActivityCompletedAsync(Guid processInstanceId, string activityId, Dictionary<string, object> variables);
}