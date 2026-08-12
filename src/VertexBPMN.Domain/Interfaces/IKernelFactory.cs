using Microsoft.SemanticKernel;

namespace VertexBPMN.Domain.Interfaces;

public interface IKernelFactory
{
    Kernel GetKernel(IDictionary<string, string> attributes);
}