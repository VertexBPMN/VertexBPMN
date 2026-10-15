namespace VertexBPMN.Domain.Exceptions;

public sealed class TaskCompletionConflictException(string message) : InvalidOperationException(message);
