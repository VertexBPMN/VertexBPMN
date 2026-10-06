namespace VertexBPMN.Sdk;
public sealed record CaseRunResult(Guid CaseInstanceId, string CaseDefinitionId, string Key, string State, IReadOnlyList<string> Trace);
