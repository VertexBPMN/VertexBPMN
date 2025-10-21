using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Input set, as per Figure 10.62.
/// </summary>
public record InputSet(
    string? Name = null,
    List<DataInput> DataInputRefs = null!,
    List<DataInput> OptionalInputRefs = null!,
    List<DataInput> WhileExecutingInputRefs = null!,
    List<OutputSet> OutputSetRefs = null!
) : BaseElement;