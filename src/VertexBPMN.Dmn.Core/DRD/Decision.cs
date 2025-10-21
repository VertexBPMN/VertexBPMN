using VertexBPMN.Domain.Model.Dmn.BusinessContext;
using VertexBPMN.Domain.Model.Dmn.Expressions;
using VertexBPMN.Domain.Model.Dmn.External;
using VertexBPMN.Domain.Model.Dmn.Requirements;
using Task = VertexBPMN.Domain.Model.Dmn.External.Task;

namespace VertexBPMN.Domain.Model.Dmn.DRD;

public sealed class Decision : DRGElement
{
    public string? Question { get; set; }
    public string? AllowedAnswers { get; set; }

    public InformationItem Variable { get; set; } = new();

    public Expressions.Expression? DecisionLogic { get; set; }

    public List<InformationRequirement> InformationRequirements { get; } = new();
    public List<KnowledgeRequirement> KnowledgeRequirements { get; } = new();
    public List<AuthorityRequirement> AuthorityRequirements { get; } = new();

    public List<Objective> SupportedObjectives { get; } = new();
    public List<PerformanceIndicator> ImpactedPerformanceIndicators { get; } = new();
    public List<OrganisationalUnit> DecisionMaker { get; } = new();
    public List<OrganisationalUnit> DecisionOwner { get; } = new();

    public List<Process> UsingProcesses { get; } = new();
    public List<Task> UsingTasks { get; } = new();
}