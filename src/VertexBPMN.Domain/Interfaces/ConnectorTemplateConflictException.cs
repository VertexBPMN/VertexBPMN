using System.Text.Json.Serialization;

namespace VertexBPMN.Domain.Interfaces;

public sealed class ConnectorTemplateConflictException(string message) : Exception(message);
