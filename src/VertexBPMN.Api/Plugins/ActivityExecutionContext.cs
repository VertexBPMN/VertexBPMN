namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public class ActivityExecutionContext
{
    public Guid ProcessInstanceId { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, object> Configuration { get; set; } = new(StringComparer.Ordinal);
}
