namespace VertexBPMN.Domain.Exceptions;

public sealed class ApplicationForeignTenantException(string message) : Exception(message);
