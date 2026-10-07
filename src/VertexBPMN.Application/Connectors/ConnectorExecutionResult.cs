using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Connectors;

public sealed record ConnectorExecutionResult(
	bool Success,
	int? StatusCode,
	IReadOnlyDictionary<string, object> Outputs,
	string? ErrorCode = null,
	int Attempts = 1,
	long DurationMilliseconds = 0);
