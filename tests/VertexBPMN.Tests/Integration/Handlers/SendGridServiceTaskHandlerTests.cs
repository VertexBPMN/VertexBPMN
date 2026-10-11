using Microsoft.Extensions.Logging;
using System.Text.Json;
using Moq;
using SendGrid;
using SendGrid.Helpers.Mail;
using VertexBPMN.Application;
using VertexBPMN.Application.Fakes;

namespace VertexBPMN.Tests.Integration.Handlers;

public class SendGridServiceTaskHandlerTests
{
	[Fact]
	public async Task ExecuteAsync_SendsEmailSuccessfully()
	{
		// Arrange
		var mockClient = new Mock<ISendGridClient>();
		SendGridMessage? sentMessage = null;
		CancellationToken sentCancellation = default;
		mockClient
			.Setup(client => client.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
			.Callback<SendGridMessage, CancellationToken>((message, cancellation) => { sentMessage = message; sentCancellation = cancellation; })
			.ReturnsAsync(new Response(System.Net.HttpStatusCode.OK, null, null));
		var handler = new SendGridServiceTaskHandler(mockClient.Object);
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "subject", "Test Subject" },
			{ "body", "Test Body" }
		};
		var variables = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			{ "apiKey", "test-api-key" },
			{ "to.email", "recipient@example.com" },
			{ "from.email", "sender@example.com" }
		};

		// Act
		await handler.ExecuteAsync(attributes, variables, TestContext.Current.CancellationToken);

		mockClient.Verify(client => client.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Once());
		Assert.NotNull(sentMessage);
		using var payload = JsonDocument.Parse(sentMessage.Serialize());
		var envelope = payload.RootElement;
		var personalization = Assert.Single(envelope.GetProperty("personalizations").EnumerateArray());
		Assert.Equal("Test Subject", personalization.GetProperty("subject").GetString());
		Assert.Equal("sender@example.com", envelope.GetProperty("from").GetProperty("email").GetString());
		Assert.Equal("recipient@example.com", Assert.Single(personalization.GetProperty("to").EnumerateArray()).GetProperty("email").GetString());
		var contents = envelope.GetProperty("content").EnumerateArray().ToArray();
		Assert.Equal(2, contents.Length);
		Assert.Equal("Test Body", Assert.Single(contents, content => content.GetProperty("type").GetString() == "text/plain").GetProperty("value").GetString());
		Assert.Equal("Test Body", Assert.Single(contents, content => content.GetProperty("type").GetString() == "text/html").GetProperty("value").GetString());
		Assert.Equal(TestContext.Current.CancellationToken, sentCancellation);

	}
	[Fact]
	public async Task ExecuteAsync_SendsEmail_Error_Handling()
	{
		// Arrange
		var mockClient = new Mock<ISendGridClient>();
		mockClient
			.Setup(client => client.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new Response(System.Net.HttpStatusCode.Unauthorized, null, null));

		var handler = new SendGridServiceTaskHandler(mockClient.Object);
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{ "subject", "Test Subject" },
			{ "body", "Test Body" }
		};
		var variables = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			{ "apiKey", "test-api-key" },
			{ "to.email", "recipient@example.com" },
			{ "from.email", "sender@example.com" }
		};

		// Act
		await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(attributes, variables, TestContext.Current.CancellationToken));

	}

	[Fact]
	public async Task SendEmailAsync_ShouldLogAndSimulateSuccess()
	{
		// Arrange
		var loggerMock = new Mock<ILogger<FakeSendGridClient>>();
		var client = new FakeSendGridClient(loggerMock.Object);
		var message = new SendGridMessage
		{
			From = new EmailAddress("test@example.com", "Test Sender"),
			Subject = "Test Email",
			HtmlContent = "<p>This is a test email.</p>"
		};
		message.AddTo(new EmailAddress("recipient@example.com", "Test Recipient"));

		// Act
		var response = await client.SendEmailAsync(message, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
		//loggerMock.Verify(
		//    x => x.Log(
		//        LogLevel.Information,
		//        It.IsAny<EventId>(),
		//        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("POL12345")),
		//        It.IsAny<Exception>(),
		//        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
		//    Times.Exactly(2));
	}
}
