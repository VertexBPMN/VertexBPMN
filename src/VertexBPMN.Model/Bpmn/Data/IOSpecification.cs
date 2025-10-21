using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record IOSpecification : BaseElement
{
    public List<DataInput> DataInputs { get; } = [];
    public List<DataOutput> DataOutputs { get; } = [];
    public List<InputSet> InputSets { get; } = [];
    public List<OutputSet> OutputSets { get; } = [];
}