namespace VertexBPMN.Domain.Entities;
public sealed record ApplicationDefinitionWriteRequest(string Key, string Name, int ExpectedVersion, string Content);
