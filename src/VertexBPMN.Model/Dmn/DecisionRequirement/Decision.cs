using VertexBPMN.Domain.Model.Bpmn.Process;
using VertexBPMN.Domain.Model.Dmn.BMM;
using VertexBPMN.Domain.Model.Dmn.Core;
using Task = VertexBPMN.Domain.Model.Bpmn.Process.Task;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// Decision (Figure 6-13, extends DRGElement).
/// </summary>
public record Decision(
    string? Question,
    string? AllowedAnswers,
    InformationItem Variable,
    Core.Expression? DecisionLogic = null,
    List<InformationRequirement> InformationRequirements = null!,
    List<KnowledgeRequirement> KnowledgeRequirements = null!,
    List<AuthorityRequirement> AuthorityRequirements = null!,
    List<BMMObjective> SupportedObjectives = null!,
    List<PerformanceIndicator> ImpactedPerformanceIndicators = null!,
    List<OrganisationalUnit> DecisionMakers = null!,
    List<OrganisationalUnit> DecisionOwners = null!,
    List<Process> UsingProcesses = null!,
    List<Task> UsingTasks = null!
) : DRGElement();