namespace VertexBPMN.Domain.Interfaces;

public sealed class FormDefinitionConflictException(string message) : Exception(message);
