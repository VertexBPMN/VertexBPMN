using System.Reflection;
using System.Runtime.Loader;

namespace VertexBPMN.Api.Plugins;

internal class PluginAssemblyLoadContext : AssemblyLoadContext
{
    public PluginAssemblyLoadContext(string pluginId) : base($"Plugin_{pluginId}", true)
    {
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Implement custom assembly resolution logic
        return null;
    }
}
