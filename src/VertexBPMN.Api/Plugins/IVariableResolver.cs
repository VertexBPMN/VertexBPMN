namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public interface IVariableResolver
{
    Task<object?> ResolveVariableAsync(string variableName, Dictionary<string, object> context);
    Task<bool> CanResolveAsync(string variableName);
}
