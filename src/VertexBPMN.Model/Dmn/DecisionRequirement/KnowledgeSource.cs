namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// KnowledgeSource (Figure 6-18, extends DRGElement).
/// </summary>
public record KnowledgeSource(
    string? LocationUri = null,
    string? Type = null,
    OrganisationalUnit? Owner = null,
    List<AuthorityRequirement> AuthorityRequirements = null!
) : DRGElement();