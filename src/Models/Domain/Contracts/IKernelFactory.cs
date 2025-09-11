using System.Collections.Generic;
using Microsoft.SemanticKernel;

namespace VertexBPMN.Domain.Contracts;

public interface IKernelFactory
{
    Kernel GetKernel(IDictionary<string, string> attributes);
}