using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class IOSpecification : BaseElement
{
    public IReadOnlyList<DataInput> DataInputs { get; } = [];
    public IReadOnlyList<DataOutput> DataOutputs { get; } = [];
    public IReadOnlyList<InputSet> InputSets { get; } = [];
    public IReadOnlyList<OutputSet> OutputSets { get; } = [];
}