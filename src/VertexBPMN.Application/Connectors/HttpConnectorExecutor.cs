using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

public class HttpConnectorExecutor(HttpClient client, HttpDataResponseReader? dataResponseReader = null) : IConnectorExecutor
{
	public virtual string Type => "http";

	public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		if (context.Endpoint is null || (context.Endpoint.Scheme != Uri.UriSchemeHttp && context.Endpoint.Scheme != Uri.UriSchemeHttps))
		{
			throw new ServiceTaskExecutionException($"{Type} connector requires an absolute HTTP(S) endpoint.");
		}

		var method = context.Attributes.TryGetValue("vertex:connector.method", out var configured) ? configured : HttpMethod.Post.Method;
		var endpoint = BuildUri(context);
		using var request = new HttpRequestMessage(new HttpMethod(method), endpoint);

		TrySetBody(request, context.Attributes, context.Variables);

		if (!string.IsNullOrEmpty(context.CredentialSecret))
		{
			var scheme = context.Attributes.TryGetValue("vertex:connector.authScheme", out var configuredScheme) ? configuredScheme : "Bearer";
			request.Headers.Authorization = new AuthenticationHeaderValue(scheme, context.CredentialSecret);
		}

		AppendBindingHeaders(request, context.Attributes, context.Variables);

		using var timeoutCts = CreateTimeoutSource(context, cancellationToken);
		using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
		var outputs = CollectOutputs(context.Attributes, response);
		await AddDataResponseFieldsAsync(outputs, response, timeoutCts.Token);

		return new ConnectorExecutionResult(
			response.IsSuccessStatusCode,
			(int)response.StatusCode,
			outputs,
			response.IsSuccessStatusCode ? null : MapHttpError(response.StatusCode));
	}

	// LC03: bound and read the response body (JSON/text), apply size limit + deep
	// redaction, and expose the fields so they can drive the process path. Errors
	// (too large, invalid body) produce a defined failure without partial mapping.
	private async Task AddDataResponseFieldsAsync(
		Dictionary<string, object> outputs,
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (dataResponseReader is null)
		{
			return;
		}

		var data = await dataResponseReader.ReadAsync(response, cancellationToken);
		if (!data.Success)
		{
			throw new ServiceTaskExecutionException($"HTTP response could not be read: {data.ErrorCode}.");
		}

		outputs["httpContentType"] = data.ContentType ?? string.Empty;
		outputs["httpBody"] = data.RawBody;
		foreach (var (key, value) in data.Fields)
		{
			outputs["httpBody." + key] = value;
		}
	}

	private static void TrySetBody(HttpRequestMessage request, IReadOnlyDictionary<string, string> attributes, IDictionary<string, object> variables)
	{
		// Structured body: each vertex:connector.bodyField.<name> becomes a JSON property.
		// Values go through ConnectorValueResolver so {{var}} references are resolved with
		// proper JSON escaping (never unchecked string interpolation).
		const string fieldPrefix = "vertex:connector.bodyField.";
		var fields = attributes
			.Where(kv => kv.Key.StartsWith(fieldPrefix, StringComparison.Ordinal))
			.Select(kv => new { Name = kv.Key[fieldPrefix.Length..], Value = ConnectorValueResolver.Resolve(kv.Value, variables, $"body field '{kv.Key[fieldPrefix.Length..]}'") })
			.ToList();

		if (fields.Count > 0)
		{
			request.Content = new StringContent(
				JsonSerializer.Serialize(fields.ToDictionary(f => f.Name, f => (object)f.Value, StringComparer.Ordinal)),
				Encoding.UTF8,
				"application/json");
			return;
		}

		if (!attributes.TryGetValue("vertex:connector.body", out var body))
		{
			return;
		}

		var resolvedBody = ConnectorValueResolver.Resolve(body, variables, "connector body");
		var contentType = attributes.TryGetValue("vertex:connector.contentType", out var configured) ? configured : "application/json";
		request.Content = new StringContent(resolvedBody, Encoding.UTF8, contentType);
	}

	// LC03: build the request URI from the endpoint template (path placeholders) plus
	// mapped query parameters. Both path and query values are resolved and always
	// percent-encoded — nothing is spliced into the URL unchecked.
	private static Uri BuildUri(ConnectorExecutionContext context)
	{
		var path = context.Endpoint!.ToString();
		foreach (var pair in context.Variables)
		{
			path = path.Replace("{" + pair.Key + "}", Uri.EscapeDataString(Convert.ToString(pair.Value) ?? string.Empty), StringComparison.Ordinal);
		}

		var builder = new UriBuilder(path);
		const string queryPrefix = "vertex:connector.query.";
		var queryItems = new List<string>();
		foreach (var attribute in context.Attributes)
		{
			if (!attribute.Key.StartsWith(queryPrefix, StringComparison.Ordinal))
			{
				continue;
			}

			var name = attribute.Key[queryPrefix.Length..];
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ServiceTaskExecutionException("Connector query parameter name must not be empty.");
			}

			var resolved = ConnectorValueResolver.Resolve(attribute.Value, context.Variables, $"query parameter '{name}'");
			queryItems.Add(Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(resolved));
		}

		if (queryItems.Count > 0)
		{
			builder.Query = string.Join("&", queryItems);
		}

		return builder.Uri;
	}

	private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase)
	{
		"Authorization",
		"Proxy-Authorization",
		"Host",
		"Connection",
		"Content-Length"
	};

	private static readonly HashSet<string> BlockedResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
	{
		"Set-Cookie",
		"Cookie"
	};

	private static void AppendBindingHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> attributes, IDictionary<string, object> variables)
	{
		const string prefix = "vertex:connector.header.";
		foreach (var attribute in attributes)
		{
			if (!attribute.Key.StartsWith(prefix, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(attribute.Value))
			{
				continue;
			}

			var headerName = attribute.Key[prefix.Length..];
			if (BlockedHeaders.Contains(headerName))
			{
				throw new ServiceTaskExecutionException($"Binding header '{headerName}' is not allowed.");
			}

			var value = ConnectorValueResolver.Resolve(attribute.Value, variables, $"binding header '{headerName}'");
			if (ContainsCtl(headerName) || ContainsCtl(value))
			{
				throw new ServiceTaskExecutionException($"Binding header '{headerName}' contains control characters.");
			}

			request.Headers.TryAddWithoutValidation(headerName, value);
		}
	}

	private static bool ContainsCtl(string value) => value.Any(char.IsControl);

	private static CancellationTokenSource CreateTimeoutSource(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var timeoutMs = ReadTimeoutMs(context);
		if (timeoutMs > 0)
		{
			source.CancelAfter(timeoutMs);
		}

		return source;
	}

	private static int ReadTimeoutMs(ConnectorExecutionContext context)
	{
		if (context.Attributes.TryGetValue("vertex:connector.timeoutMs", out var configured) && int.TryParse(configured, out var parsed))
		{
			return parsed;
		}

		var effective = context.RetryPolicy.EffectiveTimeout.TotalMilliseconds;
		return effective > 0 ? (int)effective : Timeout.Infinite;
	}

	private static Dictionary<string, object> CollectOutputs(IReadOnlyDictionary<string, string> attributes, HttpResponseMessage response)
	{
		var outputs = new Dictionary<string, object>(StringComparer.Ordinal) { ["httpStatus"] = (int)response.StatusCode };
		const string echoPrefix = "vertex:connector.outputHeader.";
		foreach (var (key, _) in attributes)
		{
			if (!key.StartsWith(echoPrefix, StringComparison.Ordinal))
			{
				continue;
			}

			var headerName = key[echoPrefix.Length..];
			if (BlockedResponseHeaders.Contains(headerName))
			{
				continue; // never export credential/session-carrying response headers into process variables
			}

			if (response.Headers.TryGetValues(headerName, out var values))
			{
				outputs[headerName] = string.Join(", ", values);
			}
		}

		return outputs;
	}

	private static string MapHttpError(HttpStatusCode status) => status switch
	{
		HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "authentication_error",
		HttpStatusCode.TooManyRequests => "rate_limited",
		>= HttpStatusCode.InternalServerError => "remote_server_error",
		_ => "http_error"
	};
}
