using System.Collections.Generic;
using VertexBPMN.Domain.Modeling;

namespace VertexBPMN.Domain.Contracts;

public interface IProcessEngine
{
    List<string> Execute(BpmnModel model);
}