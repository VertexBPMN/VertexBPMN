using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Diagram;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

#nullable enable

/// <summary>
/// Definitions (Figure 6-12, extends NamedElement).
/// </summary>
public record Definitions: NamedElement
{
    public string Namespace { get; set; }
    public string? ExpressionLanguage { get; set; } 
    public string? TypeLanguage { get; set; }
    public string? Exporter  { get; set; }
    public string? ExporterVersion { get; set; } 
    public List<DRGElement> DrgElements { get; set; } = [];
    public List<ElementCollection> ElementCollections { get; set; } = [];
    public List<ItemDefinition> ItemDefinitions { get; set; } = [];
    public List<BusinessContextElement> BusinessContextElements { get; set; } = [];
    public List<Import> Imports { get; set; } = [];
    public List<Artifact> Artifacts { get; set; } = [];
    public DMNDI? DmnDi { get; set; }
}