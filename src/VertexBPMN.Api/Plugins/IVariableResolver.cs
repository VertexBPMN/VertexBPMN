namespace VertexBPMN.Api.Plugins;

public interface IVariableResolver
{
    Task<object?> ResolveVariableAsync(string variableName, Dictionary<string, object> context);
    Task<bool> CanResolveAsync(string variableName);
}