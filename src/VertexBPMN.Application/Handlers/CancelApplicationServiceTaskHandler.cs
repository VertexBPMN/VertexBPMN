using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Handlers;

public class CancelApplicationServiceTaskHandler(
    ILogger<CancelApplicationServiceTaskHandler> logger) : IServiceTaskHandler
{
    private readonly ILogger<CancelApplicationServiceTaskHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task ExecuteAsync(
        IDictionary<string, string> attributes,
        IDictionary<string, object> variables,
        CancellationToken ct = default)
    {
        var applicationId = RequiredVariable(variables, "applicationId");
        var reason = RequiredVariable(variables, "reason");

        _logger.LogInformation(
            "Cancelling application {ApplicationId} for reason: {Reason}",
            applicationId,
            reason);
        await CancelApplicationAsync(applicationId, reason, ct);
        variables["applicationStatus"] = "Cancelled";
        _logger.LogInformation("Application {ApplicationId} successfully cancelled.", applicationId);
    }

    private Task CancelApplicationAsync(string applicationId, string reason, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "Simulating cancellation of application {ApplicationId} with reason: {Reason}",
            applicationId,
            reason);
        return Task.CompletedTask;
    }

    private static string RequiredVariable(IDictionary<string, object> variables, string name) =>
        variables.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
            ? value.ToString()!
            : throw new InvalidOperationException($"Missing or invalid '{name}' variable.");
}
