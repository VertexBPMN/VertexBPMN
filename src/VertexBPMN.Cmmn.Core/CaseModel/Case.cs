using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;
using VertexBPMN.Domain.Model.Cmmn.InformationModel;
using VertexBPMN.Domain.Model.Cmmn.PlanModel;

namespace VertexBPMN.Domain.Model.Cmmn.CaseModel;

public sealed class Case : CmmnElement
{
    public CaseFile? CaseFileModel { get; set; }
    public CasePlanModel? CasePlanModel { get; set; }
    public Collection<Role> Roles { get; } = new();
    public Collection<PlanModel.CaseParameter> Inputs { get; } = new();
    public Collection<PlanModel.CaseParameter> Outputs { get; } = new();
}