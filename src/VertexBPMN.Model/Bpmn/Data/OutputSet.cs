using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record OutputSet : BaseElement
{
    public List<DataOutput> DataOutputRefs { get; } = [];
}