using Microsoft.SemanticKernel;

namespace VertexBPMN.Core.Tasks;

public interface IKernelFactory
{
    Kernel GetKernel(IDictionary<string, string> attributes);
}