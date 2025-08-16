namespace VertexBPMN.Tests.Tasks;

using Microsoft.Extensions.Logging;
using Moq;
using SendGrid;
using SendGrid.Helpers.Mail;
using VertexBPMN.Core.Fakes;
using VertexBPMN.Core.Services;
using Xunit;

public class SendGridServiceTaskHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_SendsEmailSuccessfully()
    {
        // Arrange
        var mockClient = new Mock<ISendGridClient>();
        mockClient
            .Setup(client => client.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Response(System.Net.HttpStatusCode.OK, null, null));
        var handler = new SendGridServiceTaskHandler(mockClient.Object);
        var attributes = new Dictionary<string, string>
        {
            { "subject", "Test Subject" },
            { "body", "Test Body" }
        };
        var variables = new Dictionary<string, object>
        {
            { "apiKey", "test-api-key" },
            { "to.email", "recipient@example.com" },
            { "from.email", "sender@example.com" }
        };

        // Act
        await handler.ExecuteAsync(attributes, variables);

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
        var attributes = new Dictionary<string, string>
        {
            { "subject", "Test Subject" },
            { "body", "Test Body" }
        };
        var variables = new Dictionary<string, object>
        {
            { "apiKey", "test-api-key" },
            { "to.email", "recipient@example.com" },
            { "from.email", "sender@example.com" }
        };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(attributes, variables));

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
        var response = await client.SendEmailAsync(message);

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