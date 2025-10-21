using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Abstract criterion (5.4.5, inherits from CMMNElement).
/// </summary>
public abstract record Criterion(
    string? Name = null,
    Sentry? SentryRef = null
) : CMMNElement();