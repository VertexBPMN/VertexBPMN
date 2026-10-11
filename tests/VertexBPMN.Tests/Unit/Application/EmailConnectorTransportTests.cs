using System.Net.Mail;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class EmailConnectorTransportTests
{
	[Fact]
	public void RemoteTransportRequiresTlsByDefault()
	{
		using var client = new SmtpClient("mail.example.test");
		EmailConnectorExecutor.ConfigureTransport(client, Context());
		Assert.True(client.EnableSsl);
	}

	[Theory]
	[InlineData("mail.example.test", null)]
	[InlineData("192.168.1.10", null)]
	[InlineData("localhost.example.test", null)]
	[InlineData("127.0.0.1", "credential-canary")]
	[InlineData("localhost", "credential-canary")]
	public void PlaintextRejectsRemoteHostsAndCredentials(string host, string? secret)
	{
		using var client = new SmtpClient(host);
		var exception = Assert.Throws<ServiceTaskExecutionException>(
			() => EmailConnectorExecutor.ConfigureTransport(client, Context("false", secret)));
		Assert.DoesNotContain("credential-canary", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("localhost", "127.0.0.1")]
	[InlineData("127.0.0.1", "127.0.0.1")]
	[InlineData("::1", "::1")]
	public void ExplicitLocalTestingUsesLiteralLoopbackWithoutCredentials(string host, string expectedHost)
	{
		using var client = new SmtpClient(host);
		EmailConnectorExecutor.ConfigureTransport(client, Context("false"));
		Assert.False(client.EnableSsl);
		Assert.Equal(expectedHost, client.Host);
	}

	private static ConnectorExecutionContext Context(string? ssl = null, string? secret = null)
	{
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
		if (ssl is not null)
		{
			attributes["vertex:connector.ssl"] = ssl;
		}

		return new ConnectorExecutionContext("tenant-a", "email", "send", null,
			attributes, new Dictionary<string, object>(StringComparer.Ordinal),
			new ConnectorRetryPolicy(1), CredentialSecret: secret);
	}
}
