using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Input output binding, as per Figure 10.43.
/// </summary>
public record InputOutputBinding(
    Operation OperationRef,
    DataInput InputDataRef,
    DataOutput OutputDataRef
) : BaseElement;