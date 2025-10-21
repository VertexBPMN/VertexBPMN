using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Plan item control (5.4.11, inherits from CMMNElement).
/// </summary>
public record PlanItemControl(
    RepetitionRule? RepetitionRule = null,
    RequiredRule? RequiredRule = null,
    ManualActivationRule? ManualActivationRule = null
) : CMMNElement();