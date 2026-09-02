using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Handlers;

public class IssuePolicyServiceTaskHandler(
    ILogger<IssuePolicyServiceTaskHandler> logger) : IServiceTaskHandler
{
    private readonly ILogger<IssuePolicyServiceTaskHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task ExecuteAsync(
        IDictionary<string, string> attributes,
        IDictionary<string, object> variables,
        CancellationToken ct = default)
    {
        var policyId = RequiredVariable(variables, "policyId");
        var customerId = RequiredVariable(variables, "customerId");

        _logger.LogInformation("Issuing policy {PolicyId} for customer {CustomerId}", policyId, customerId);
        await IssuePolicyAsync(policyId, customerId, ct);
        variables["policyStatus"] = "Issued";
        _logger.LogInformation(
            "Policy {PolicyId} successfully issued for customer {CustomerId}.",
            policyId,
            customerId);
    }

    private Task IssuePolicyAsync(string policyId, string customerId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "Simulating issuance of policy {PolicyId} for customer {CustomerId}",
            policyId,
            customerId);
        return Task.CompletedTask;
    }

    private static string RequiredVariable(IDictionary<string, object> variables, string name) =>
        variables.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
            ? value.ToString()!
            : throw new InvalidOperationException($"Missing or invalid '{name}' variable.");
}
