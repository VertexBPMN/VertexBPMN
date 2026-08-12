using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class Decision : CmmnElement
{
    public string? Name { get; set; }
    public UriString? ImplementationType { get; set; }
    public Qname? ExternalRef { get; set; }
    public Collection<DecisionParameter> Inputs { get; } = new();
    public Collection<DecisionParameter> Outputs { get; } = new();
}