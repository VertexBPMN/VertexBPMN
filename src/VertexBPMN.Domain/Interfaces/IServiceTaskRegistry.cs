namespace VertexBPMN.Domain.Interfaces;

public interface IServiceTaskRegistry
{
	void Register(string implementation, IServiceTaskHandler handler);
	bool Remove(string implementation);
	bool TryResolve(string implementation, out IServiceTaskHandler? handler);
	IServiceTaskHandler GetHandler(string type);
}
