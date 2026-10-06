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
			EnableSsl = !context.Attributes.TryGetValue("vertex:connector.ssl", out var ssl) || !string.Equals(ssl, "false", StringComparison.OrdinalIgnoreCase)
		};
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
	private static string Require(ConnectorExecutionContext context, string key) => context.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ServiceTaskExecutionException($"Email connector requires '{key}'.");
}
