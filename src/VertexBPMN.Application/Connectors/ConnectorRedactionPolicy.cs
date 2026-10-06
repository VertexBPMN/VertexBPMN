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

public sealed class ConnectorRedactionPolicy
{
	private static readonly string[] SensitiveFragments = ["secret", "token", "password", "authorization", "apikey", "api-key", "connectionstring"];
	public bool IsSensitive(string key) => SensitiveFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
	public IReadOnlyDictionary<string, object> Redact(IReadOnlyDictionary<string, object> values) =>
		values.ToDictionary(pair => pair.Key, pair => IsSensitive(pair.Key) ? (object)"***" : pair.Value, StringComparer.OrdinalIgnoreCase);
}
