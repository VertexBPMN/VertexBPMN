using SendGrid;
using SendGrid.Helpers.Mail;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

public class SendGridServiceTaskHandler(ISendGridClient client) : IServiceTaskHandler
{

	public async Task ExecuteAsync(
		IDictionary<string, string> attributes,
		IDictionary<string, object> variables,
		CancellationToken ct = default)
	{
		// Extrahiere notwendige Variablen
		// Preserve the legacy input validation; the injected client owns authentication.
		_ = GetVariable(variables, "apiKey");
		var toEmail = GetVariable(variables, "to.email");
		var fromEmail = GetVariable(variables, "from.email");
		var subject = GetAttribute(attributes, "subject", "Default Subject");
		var body = GetAttribute(attributes, "body", "Default Body");

		var from = new EmailAddress(fromEmail, "Sender Name");
		var to = new EmailAddress(toEmail, "Recipient Name");
		var msg = MailHelper.CreateSingleEmail(from, to, subject, body, body);

		// Sende die E-Mail
		var response = await client.SendEmailAsync(msg, ct);

		// Fehlerbehandlung
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Failed to send email. Status Code: {response.StatusCode}");
		}
	}

	private static string GetVariable(IDictionary<string, object> variables, string key)
	{
		if (variables.TryGetValue(key, out var value) && value is string stringValue)
		{
			return stringValue;
		}
		throw new KeyNotFoundException($"Variable '{key}' is missing or not a string.");
	}

	private static string GetAttribute(IDictionary<string, string> attributes, string key, string defaultValue)
	{
		return attributes.TryGetValue(key, out var value) ? value : defaultValue;
	}
}
