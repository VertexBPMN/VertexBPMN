using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class Process : CmmnElement
{
    public string? Name { get; set; }
    public UriString? ImplementationType { get; set; }
    public Qname? ProcessRef { get; set; }
    public Collection<ProcessParameter> Inputs { get; } = new();
    public Collection<ProcessParameter> Outputs { get; } = new();
}