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

public sealed class ConnectorRegistry(IEnumerable<IConnectorExecutor> executors) : IConnectorRegistry
{
	private readonly IReadOnlyDictionary<string, IConnectorExecutor> _executors = executors.ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);
	public IConnectorExecutor Resolve(string type) => _executors.TryGetValue(type, out var executor)
		? executor
		: throw new ServiceTaskExecutionException($"No connector executor is registered for '{type}'.");
}
