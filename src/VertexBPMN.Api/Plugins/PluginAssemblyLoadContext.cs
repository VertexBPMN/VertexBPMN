using System.Reflection;
using System.Runtime.Loader;

namespace VertexBPMN.Api.Plugins;

public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _pluginId;

    public PluginAssemblyLoadContext(string pluginId) : base($"Plugin_{pluginId}", true)
    {
        _pluginId = pluginId;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Implement custom assembly resolution logic
        return null;
    }
}