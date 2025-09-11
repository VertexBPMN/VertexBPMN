using Microsoft.SemanticKernel;

namespace VertexBPMN.Core.Services;

public interface IKernelFactory
{
    Kernel GetKernel(IDictionary<string, string> attributes);
}