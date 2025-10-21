using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Enums;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Ad hoc sub process, as per Figure 10.29.
/// </summary>
public record AdHocSubProcess(
    Expression CompletionCondition,
    AdHocOrdering Ordering = AdHocOrdering.Parallel,
    bool CancelRemainingInstances = true
) : SubProcess();