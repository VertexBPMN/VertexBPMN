using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

public class EmailConnectorExecutor : IConnectorExecutor
{
	public virtual string Type => "email";
	public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		var host = Require(context, "vertex:connector.smtpHost");
		var to = Require(context, "vertex:connector.to");
		var from = Require(context, "vertex:connector.from");
		var port = context.Attributes.TryGetValue("vertex:connector.smtpPort", out var rawPort) && int.TryParse(rawPort, out var configuredPort) ? configuredPort : 587;
		using var client = new SmtpClient(host, port)
		{
			EnableSsl = true
		};
		ConfigureTransport(client, context);
		if (!string.IsNullOrEmpty(context.CredentialSecret))
		{
			var username = Require(context, "vertex:connector.username");
			client.Credentials = new NetworkCredential(username, context.CredentialSecret);
		}
		using var message = new MailMessage(from, to)
		{
			Subject = context.Attributes.TryGetValue("vertex:connector.subject", out var subject) ? subject : string.Empty,
			Body = context.Attributes.TryGetValue("vertex:connector.body", out var body) ? body : string.Empty
		};
		await client.SendMailAsync(message, cancellationToken);
		return new ConnectorExecutionResult(true, null, new Dictionary<string, object>(StringComparer.Ordinal) { ["delivered"] = true });
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "S5332", Justification = "Explicit plaintext SMTP is restricted to literal loopback destinations without credentials; all remote connections require TLS.")]
	internal static void ConfigureTransport(SmtpClient client, ConnectorExecutionContext context)
	{
		if (!context.Attributes.TryGetValue("vertex:connector.ssl", out var ssl) || !string.Equals(ssl, "false", StringComparison.OrdinalIgnoreCase))
		{
			client.EnableSsl = true;
			return;
		}

		var host = string.Equals(client.Host, "localhost", StringComparison.OrdinalIgnoreCase) ? IPAddress.Loopback.ToString() : client.Host;
		if (!IPAddress.TryParse(host, out var address) || !IPAddress.IsLoopback(address) || !string.IsNullOrEmpty(context.CredentialSecret))
		{
			throw new ServiceTaskExecutionException("Plaintext SMTP is only allowed for local loopback testing without credentials.");
		}

		client.Host = address.ToString();
		client.EnableSsl = false;
	}
	private static string Require(ConnectorExecutionContext context, string key) => context.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ServiceTaskExecutionException($"Email connector requires '{key}'.");
}
