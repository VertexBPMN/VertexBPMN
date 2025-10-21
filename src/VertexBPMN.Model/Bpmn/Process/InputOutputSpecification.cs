using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Input output specification, as per Figure 10.57.
/// </summary>
public record InputOutputSpecification(
    List<DataInput> DataInputs = null!,
    List<DataOutput> DataOutputs = null!,
    List<InputSet> InputSets = null!,
    List<OutputSet> OutputSets = null!
) : BaseElement;