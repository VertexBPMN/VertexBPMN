namespace VertexBPMN.Domain.Interfaces;

public class NullServiceTaskRegistry : IServiceTaskRegistry
{
	public static NullServiceTaskRegistry Instance { get; } = new NullServiceTaskRegistry();
	public void Register(string implementation, IServiceTaskHandler handler)
	{

	}

	public bool Remove(string implementation) => false;

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
