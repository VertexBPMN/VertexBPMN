using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.DRD;
using VertexBPMN.Domain.Model.Dmn.Expressions;

namespace VertexBPMN.Domain.Model.Dmn.Tests;

public class Smoke
{
    public void ModelBuilds()
    {
        var defs = new Definitions { Name = "Loan", NamespaceUri = "http://example/loan" };
        var input = new InputData { Name = "Applicant", Variable = new InformationItem { Name = "Applicant", TypeRef = "tApplicant" } };
        defs.DrgElements.Add(input);
        var decision = new Decision { Name = "Eligibility", Variable = new InformationItem { Name = "Eligibility", TypeRef = "string" } };
        decision.DecisionLogic = new DecisionTable.DecisionTable
        {
            HitPolicy = HitPolicy.UNIQUE,
            PreferredOrientation = DecisionTableOrientation.RuleAsRow
        };
        defs.DrgElements.Add(decision);
    }
}