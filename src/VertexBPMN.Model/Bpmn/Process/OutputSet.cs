using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Output set, as per Figure 10.63.
/// </summary>
public record OutputSet(
    string? Name = null,
    List<DataOutput> DataOutputRefs = null!,
    List<DataOutput> OptionalOutputRefs = null!,
    List<DataOutput> WhileExecutingOutputRefs = null!,
    List<InputSet> InputSetRefs = null!
) : BaseElement;