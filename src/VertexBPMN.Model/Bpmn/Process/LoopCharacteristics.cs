using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Abstract loop characteristics, as per Figure 10.45.
/// </summary>
public abstract record LoopCharacteristics(
    Expression? TestBefore = null,
    int? LoopMaximum = null
) : BaseElement;