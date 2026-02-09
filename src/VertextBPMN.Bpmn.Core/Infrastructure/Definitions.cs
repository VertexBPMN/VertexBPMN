using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Infrastructure;

[method: SetsRequiredMembers]
public class Definitions(
    string id,
    string targetNamespace,
    string expressionLanguage = null,
    string typeLanguage = null,
    string exporter = null,
    string exporterVersion = null)
{
    public string? Id { get; set; } = id;
    public required string TargetNamespace { get; set; } = targetNamespace;
    public string? ExpressionLanguage { get; set; } = expressionLanguage;
    public string? TypeLanguage { get; set; } = typeLanguage;
    public string? Exporter { get; set; } = exporter;
    public string? ExporterVersion { get; set; } = exporterVersion;

    public IReadOnlyList<Import> Imports { get; } = [];
    public IReadOnlyList<Extension> Extensions { get; } = [];
    public IReadOnlyList<RootElement> RootElements { get; } = [];
    public IReadOnlyList<Relationship> Relationships { get; } = [];
    public IReadOnlyList<object> Diagrams { get; } = [];
}