using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public sealed record CaseExecutionResult(CaseInstanceRecord Instance, IReadOnlyList<string> Trace);
