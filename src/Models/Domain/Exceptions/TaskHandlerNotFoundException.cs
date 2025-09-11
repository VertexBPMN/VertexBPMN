using System;

namespace VertexBPMN.Domain.Exceptions;

public class TaskHandlerNotFoundException : Exception
{
    public TaskHandlerNotFoundException(string taskDefinitionType, string taskId)
        : base($"No handler found for taskDefinition '{taskDefinitionType}' in ServiceTask '{taskId}'.")
    {
    }
}