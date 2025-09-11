namespace VertexBPMN.Api.Plugins;

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