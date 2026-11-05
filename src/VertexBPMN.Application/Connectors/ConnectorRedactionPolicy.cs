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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2325", Justification = "Preserve the public instance contract of the DI-provided redaction policy.")]
	public bool IsSensitive(string key) => SensitiveFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
	public IReadOnlyDictionary<string, object> Redact(IReadOnlyDictionary<string, object> values) =>
		values.ToDictionary(pair => pair.Key, pair => RedactValue(pair.Key, pair.Value), StringComparer.OrdinalIgnoreCase);

	// LC03: deep redaction for parsed JSON response bodies, so sensitive fields
	// inside objects/lists are not captured into process variables/history/audit.
	public object RedactValue(string key, object value) =>
		value switch
		{
			Dictionary<string, object> map => Redact(map),
			System.Collections.Generic.List<object> list => list.Select(item => item is Dictionary<string, object> inner
				? (object)Redact(inner)
				: item).ToList(),
			_ when IsSensitive(key) => "***",
			_ => value
		};
}
