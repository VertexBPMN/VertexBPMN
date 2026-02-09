using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.DRD;

namespace VertexBPMN.Domain.Model.Dmn.Requirements;

public sealed class InformationRequirement : DMNElement
{
    public Decision? RequiredDecision { get; set; }
    public InputData? RequiredInput { get; set; }

    public void Validate()
    {
        if ((RequiredDecision is null) == (RequiredInput is null))
            throw new InvalidOperationException("InformationRequirement must reference exactly one of RequiredDecision or RequiredInput.");
    }
}