using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Handlers
{
    public class RejectPolicyServiceTaskHandler : IServiceTaskHandler
    {
        private readonly ILogger<RejectPolicyServiceTaskHandler> _logger;

        public RejectPolicyServiceTaskHandler(ILogger<RejectPolicyServiceTaskHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync(
            IDictionary<string, string> attributes,
            IDictionary<string, object> variables,
            CancellationToken ct = default)
        {
            // Extrahiere notwendige Variablen
            if (!variables.TryGetValue("policyId", out var policyId) || string.IsNullOrWhiteSpace(policyId?.ToString()))
            {
                throw new InvalidOperationException("Missing or invalid 'policyId' variable.");
            }

            if (!variables.TryGetValue("reason", out var reason) || string.IsNullOrWhiteSpace(reason?.ToString()))
            {
                throw new InvalidOperationException("Missing or invalid 'reason' variable.");
            }

            // Logge die Ablehnungsanfrage
            _logger.LogInformation("Rejecting policy {PolicyId} for reason: {Reason}", policyId, reason);

            // Simuliere die Logik zum Ablehnen der Police
            await RejectPolicyAsync(policyId.ToString(), reason.ToString(), ct);

            // Aktualisiere die Prozessvariablen
            variables["policyStatus"] = "Rejected";

            // Logge den Abschluss
            _logger.LogInformation("Policy {PolicyId} successfully rejected.", policyId);
        }

        private Task RejectPolicyAsync(string policyId, string reason, CancellationToken ct)
        {
            // Simulierte Logik für das Ablehnen der Police
            // In einer echten Implementierung könnte dies ein API-Aufruf oder eine Datenbankoperation sein
            _logger.LogDebug("Simulating rejection of policy {PolicyId} with reason: {Reason}", policyId, reason);
            return Task.CompletedTask;
        }
    }
}