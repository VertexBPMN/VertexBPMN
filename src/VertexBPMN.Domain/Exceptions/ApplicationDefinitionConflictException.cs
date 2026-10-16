namespace VertexBPMN.Domain.Exceptions;

public sealed class ApplicationDefinitionConflictException(string message) : Exception(message);
