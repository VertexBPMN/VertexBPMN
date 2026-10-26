namespace VertexBPMN.Studio.Services;

/// <summary>
/// One explicit action authored on a component in the low-code app designer (LC07 S3,
/// Button-Ausführung). It carries the released LC06 action identity (<c>Type</c> from the
/// closed action catalog + <c>Version</c> 1.0.0), a reference-based <see cref="Target"/>
/// (a declared data source key for data-source-read, a stable process KEY for start-process,
/// or a task key for claim/complete-task) and explicit <see cref="Inputs"/>.
/// </summary>
internal sealed class DesignerAction(string type, string version, string? target, List<DesignerActionInput> inputs)
{
    public string Type { get; set; } = type;
    public string Version { get; } = version;
    public string? Target { get; set; } = target;
    public List<DesignerActionInput> Inputs { get; } = inputs;
}
