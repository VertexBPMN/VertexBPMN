using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>Immutable version references and safe schema snapshot, never provider credentials.</summary>
public sealed record ResolvedExternalTaskContract(string Version, string? AgentProfileVersion, string SchemaSnapshot);
