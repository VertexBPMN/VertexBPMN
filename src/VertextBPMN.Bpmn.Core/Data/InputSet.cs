using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class InputSet : BaseElement
{
    public IReadOnlyList<DataInput> DataInputRefs { get; } = [];
}