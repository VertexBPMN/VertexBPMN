namespace VertexBPMN.Api.Plugins;

public interface ICustomActivityBehavior
{
    Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context);
    Task<bool> CanExecuteAsync(ActivityExecutionContext context);
}