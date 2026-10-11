using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Observability;

internal sealed record LogEntry(LogLevel Level, string Message, string? StructuredState);
