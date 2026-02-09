using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.InformationModel;

public sealed class CaseFileItemDefinition : CmmnElement
{
    public string? StructureRef { get; set; }
    public Collection<Property> Properties { get; } = new();
}