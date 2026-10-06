using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Handlers;

public class RejectPolicyServiceTaskHandler(
	ILogger<RejectPolicyServiceTaskHandler> logger) : IServiceTaskHandler
{
	private readonly ILogger<RejectPolicyServiceTaskHandler> _logger =
		logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task ExecuteAsync(
		IDictionary<string, string> attributes,
		IDictionary<string, object> variables,
		CancellationToken ct = default)
	{
		var policyId = RequiredVariable(variables, "policyId");
		var reason = RequiredVariable(variables, "reason");

		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Rejecting policy {PolicyId} for reason: {Reason}", policyId, reason);
		}
		await RejectPolicyAsync(policyId, reason, ct);
		variables["policyStatus"] = "Rejected";
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Policy {PolicyId} successfully rejected.", policyId);
		}
	}

	private Task RejectPolicyAsync(string policyId, string reason, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
		{
			_logger.LogDebug(
			"Simulating rejection of policy {PolicyId} with reason: {Reason}",
			policyId,
			reason);
		}
		return Task.CompletedTask;
	}

	private static string RequiredVariable(IDictionary<string, object> variables, string name) =>
		variables.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
			? value.ToString()!
			: throw new InvalidOperationException($"Missing or invalid '{name}' variable.");
}
