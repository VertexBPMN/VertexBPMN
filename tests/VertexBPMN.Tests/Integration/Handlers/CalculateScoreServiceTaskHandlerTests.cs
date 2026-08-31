
using VertexBPMN.Application.Handlers;

namespace VertexBPMN.Tests.Integration.Handlers;

public class CalculateScoreServiceTaskHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCalculateCreditScore()
    {
        // Arrange
        var handler = new CalculateScoreServiceTaskHandler();
        var variables = new Dictionary<string, object>
        {
            { "applicantName", "John Doe" },
            { "age", 30 }
        };

        // Act
        await handler.ExecuteAsync(new Dictionary<string, string>(), variables, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(variables.ContainsKey("creditScore"));
        Assert.Equal(560, variables["creditScore"]); // Beispiel-Ergebnis
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenApplicantNameIsMissing()
    {
        // Arrange
        var handler = new CalculateScoreServiceTaskHandler();
        var variables = new Dictionary<string, object>
        {
            { "age", 30 }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(new Dictionary<string, string>(), variables, TestContext.Current.CancellationToken));
    }
}