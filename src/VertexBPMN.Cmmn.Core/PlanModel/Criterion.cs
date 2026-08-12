using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public abstract class Criterion : CmmnElement
{
    public Sentry? SentryRef { get; set; }
}