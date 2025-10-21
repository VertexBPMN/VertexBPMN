namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// OrganisationalUnit (extends BusinessContextElement).
/// </summary>
public record OrganisationalUnit(
    List<Decision> DecisionsMade = null!,
    List<Decision> DecisionsOwned = null!
) : BusinessContextElement();