namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public interface IProcessExecutionListener
{
    Task OnProcessStartedAsync(Guid processInstanceId, Dictionary<string, object> variables);
    Task OnProcessCompletedAsync(Guid processInstanceId, Dictionary<string, object> variables);
    Task OnActivityStartedAsync(Guid processInstanceId, string activityId, Dictionary<string, object> variables);
    Task OnActivityCompletedAsync(Guid processInstanceId, string activityId, Dictionary<string, object> variables);
}
