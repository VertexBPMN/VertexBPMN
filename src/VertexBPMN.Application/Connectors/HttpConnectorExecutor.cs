using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
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
		if (context.Attributes.TryGetValue("vertex:connector.body", out var body))
		{
			request.Content = new StringContent(body, Encoding.UTF8, context.Attributes.TryGetValue("vertex:connector.contentType", out var contentType) ? contentType : "application/json");
		}

		if (!string.IsNullOrEmpty(context.CredentialSecret))
		{
			var scheme = context.Attributes.TryGetValue("vertex:connector.authScheme", out var configuredScheme) ? configuredScheme : "Bearer";
			request.Headers.Authorization = new AuthenticationHeaderValue(scheme, context.CredentialSecret);
		}

		using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		return new ConnectorExecutionResult(
			response.IsSuccessStatusCode,
			(int)response.StatusCode,
			new Dictionary<string, object>(StringComparer.Ordinal) { ["httpStatus"] = (int)response.StatusCode },
			response.IsSuccessStatusCode ? null : MapHttpError(response.StatusCode));
	}


	private static Uri ResolveEndpoint(Uri template, IDictionary<string, object> variables)
	{
		var path = template.ToString();
		foreach (var pair in variables)
		{
			path = path.Replace("{" + pair.Key + "}", Uri.EscapeDataString(Convert.ToString(pair.Value) ?? string.Empty));
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
