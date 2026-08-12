using VertexBPMN.Domain.Model.Dmn.Artifacts;
using VertexBPMN.Domain.Model.Dmn.BusinessContext;
using VertexBPMN.Domain.Model.Dmn.DI;
using VertexBPMN.Domain.Model.Dmn.DRD;
using VertexBPMN.Domain.Model.Dmn.Expressions;

namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>Definitions (6.3.2)</summary>
public sealed class Definitions : NamedElement
{
    public string NamespaceUri { get; set; } = string.Empty;
    public Uri? ExpressionLanguage { get; set; }
    public Uri? TypeLanguage { get; set; }
    public string? Exporter { get; set; }
    public string? ExporterVersion { get; set; }

    public List<ItemDefinition> ItemDefinitions { get; } = new();
    public List<DRGElement> DrgElements { get; } = new();
    public List<BusinessContextElement> BusinessContextElements { get; } = new();
    public List<ElementCollection> ElementCollections { get; } = new();
    public List<Import> Imports { get; } = new();
    public List<Artifact> Artifacts { get; } = new();

    public DMNDI? DmnDi { get; set; }
}