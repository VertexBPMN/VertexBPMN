using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class Sentry : CmmnElement
{
    public IfPart? IfPart { get; set; }
    public Collection<OnPart> OnParts { get; } = new();
}