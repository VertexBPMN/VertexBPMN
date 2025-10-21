using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn.Diagram;
using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Infrastructure;

[method: SetsRequiredMembers]
public record Definitions{
    public string? Id { get; set; }
    public required string TargetNamespace { get; set; }
    public string? ExpressionLanguage { get; set; }
    public string? TypeLanguage { get; set; }
    public string? Exporter { get; set; }
    public string? ExporterVersion { get; set; }
    public List<Import> Imports { get; set; } = [];
    public List<Extension> Extensions { get; set; } = [];
    public List<RootElement> RootElements { get; set; } = [];
    public List<Relationship> Relationships { get; set; } = [];
    public List<BPMNDiagram> Diagrams { get; set; } = [];
}

