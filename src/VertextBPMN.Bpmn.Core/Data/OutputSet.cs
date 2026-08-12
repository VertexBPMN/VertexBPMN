using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class OutputSet : BaseElement
{
    public IReadOnlyList<DataOutput> DataOutputRefs { get; } = [];
}