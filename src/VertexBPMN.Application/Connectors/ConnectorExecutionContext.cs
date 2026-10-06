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

public sealed record ConnectorExecutionContext(
	string TenantId,
	string Type,
	string OperationId,
	Uri? Endpoint,
	IReadOnlyDictionary<string, string> Attributes,
	IDictionary<string, object> Variables,
	ConnectorRetryPolicy RetryPolicy,
	string? CredentialId = null,
	string? CredentialSecret = null);
