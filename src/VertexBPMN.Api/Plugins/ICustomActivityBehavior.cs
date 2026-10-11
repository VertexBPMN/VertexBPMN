namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public interface ICustomActivityBehavior
{
    Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context);
    Task<bool> CanExecuteAsync(ActivityExecutionContext context);
}
