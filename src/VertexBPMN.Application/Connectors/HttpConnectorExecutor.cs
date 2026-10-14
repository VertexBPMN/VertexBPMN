using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

public class HttpConnectorExecutor(HttpClient client) : IConnectorExecutor
{
	public virtual string Type => "http";

	public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		if (context.Endpoint is null || (context.Endpoint.Scheme != Uri.UriSchemeHttp && context.Endpoint.Scheme != Uri.UriSchemeHttps))
		{
			throw new ServiceTaskExecutionException($"{Type} connector requires an absolute HTTP(S) endpoint.");
		}

		var method = context.Attributes.TryGetValue("vertex:connector.method", out var configured) ? configured : HttpMethod.Post.Method;
		var endpoint = ResolveEndpoint(context.Endpoint, context.Variables);
		using var request = new HttpRequestMessage(new HttpMethod(method), endpoint);

		TrySetBody(request, context.Attributes);

		if (!string.IsNullOrEmpty(context.CredentialSecret))
		{
			var scheme = context.Attributes.TryGetValue("vertex:connector.authScheme", out var configuredScheme) ? configuredScheme : "Bearer";
			request.Headers.Authorization = new AuthenticationHeaderValue(scheme, context.CredentialSecret);
		}

		AppendBindingHeaders(request, context.Attributes);

		using var timeoutCts = CreateTimeoutSource(context, cancellationToken);
		using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
		var outputs = CollectOutputs(context.Attributes, response);

		return new ConnectorExecutionResult(
			response.IsSuccessStatusCode,
			(int)response.StatusCode,
			outputs,
			response.IsSuccessStatusCode ? null : MapHttpError(response.StatusCode));
	}

	private static void TrySetBody(HttpRequestMessage request, IReadOnlyDictionary<string, string> attributes)
	{
		if (!attributes.TryGetValue("vertex:connector.body", out var body))
		{
			return;
		}

		var contentType = attributes.TryGetValue("vertex:connector.contentType", out var configured) ? configured : "application/json";
		request.Content = new StringContent(body, Encoding.UTF8, contentType);
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

	private static void AppendBindingHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> attributes)
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

			if (ContainsCtl(headerName) || ContainsCtl(attribute.Value))
			{
				throw new ServiceTaskExecutionException($"Binding header '{headerName}' contains control characters.");
			}

			request.Headers.TryAddWithoutValidation(headerName, attribute.Value);
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

	private static Uri ResolveEndpoint(Uri template, IDictionary<string, object> variables)
	{
		var path = template.ToString();
		foreach (var pair in variables)
		{
			path = path.Replace("{" + pair.Key + "}", Uri.EscapeDataString(Convert.ToString(pair.Value) ?? string.Empty), StringComparison.Ordinal);
		}

		return new Uri(path);
	}

	private static string MapHttpError(HttpStatusCode status) => status switch
	{
		HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "authentication_error",
		HttpStatusCode.TooManyRequests => "rate_limited",
		>= HttpStatusCode.InternalServerError => "remote_server_error",
		_ => "http_error"
	};
}
