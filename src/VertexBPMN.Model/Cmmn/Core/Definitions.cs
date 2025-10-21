using VertexBPMN.Domain.Model.Cmmn.Artifacts;
using VertexBPMN.Domain.Model.Cmmn.CaseModel;
using VertexBPMN.Domain.Model.Cmmn.Diagram;
using VertexBPMN.Domain.Model.Cmmn.PlanModel;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Definitions as root container (5.1.2, inherits from CMMNElement).
/// Extension: Added artifacts and decisions for integration.
/// </summary>
public record Definitions(
    string? Name = null,
    string? TargetNamespace = null,
    string? ExpressionLanguage = null,
    string? Exporter = null,
    string? ExporterVersion = null,
    string? Author = null,
    DateTime? CreationDate = null,
    List<Import> Imports = null!,
    List<CaseFileItemDefinition> CaseFileItemDefinitions = null!,
    List<Case> Cases = null!,
    List<Process> Processes = null!,
    List<Relationship> Relationships = null!,
    CMMNDI? CmmnDi = null,
    List<Decision> Decisions = null!, // Extension: DMN integration.
    List<Artifact> Artifacts = null! // Extension: Additional artifacts.
) : CMMNElement();