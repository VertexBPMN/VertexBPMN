using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Resources;

public class Resource : RootElement
{
    public required string Name { get; set; }
    public IReadOnlyList<ResourceParameter> ResourceParameters { get; } = [];
}