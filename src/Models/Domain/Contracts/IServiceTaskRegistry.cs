namespace VertexBPMN.Domain.Contracts;

public interface IServiceTaskRegistry
{
    void Register(string implementation, IServiceTaskHandler handler);
    bool TryResolve(string implementation, out IServiceTaskHandler? handler);
    IServiceTaskHandler GetHandler(string type);
}

public class NullServiceTaskRegistry : IServiceTaskRegistry
{
    private static readonly NullServiceTaskRegistry _instance = new NullServiceTaskRegistry();
    public static NullServiceTaskRegistry Instance => _instance;
    public void Register(string implementation, IServiceTaskHandler handler)
    {
       
    }

    public bool TryResolve(string implementation, out IServiceTaskHandler? handler)
    {
        handler = null;
      return false;
    }

    public IServiceTaskHandler GetHandler(string type)
    {
       return null!;
    }
}