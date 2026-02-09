using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Artifacts;

public class Category : RootElement
{
    public string? Name { get; set; }
    public IReadOnlyList<CategoryValue> CategoryValues { get; } = [];
}