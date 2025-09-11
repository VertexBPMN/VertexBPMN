using Microsoft.SemanticKernel;

namespace VertexBPMN.Core.Contracts;

public interface IKernelFactory
{
    Kernel GetKernel(IDictionary<string, string> attributes);
}