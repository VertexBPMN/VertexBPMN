namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public enum PluginStatus
{
    Loading,
    Loaded,
    Enabled,
    Disabled,
    Error,
    Unloading
}
