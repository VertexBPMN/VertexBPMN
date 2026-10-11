namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public class PluginServiceContainer
{
    private readonly Dictionary<Type, object> _services = new();

    public void RegisterService<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    public T? GetService<T>() where T : class
    {
        return _services.GetValueOrDefault(typeof(T)) as T;
    }

    public bool HasService<T>()
    {
        return _services.ContainsKey(typeof(T));
    }
}
