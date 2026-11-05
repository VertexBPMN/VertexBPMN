namespace VertexBPMN.Domain.Exceptions;

public sealed class ApplicationValidationException(string message) : Exception(message);
