namespace VertexBPMN.Api.Plugins;

public class ActivityExecutionContext
{
    public Guid ProcessInstanceId { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}