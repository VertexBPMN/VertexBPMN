using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Handlers
{
    public class IssuePolicyServiceTaskHandler : IServiceTaskHandler
    {
        private readonly ILogger<IssuePolicyServiceTaskHandler> _logger;

        public IssuePolicyServiceTaskHandler(ILogger<IssuePolicyServiceTaskHandler> logger)
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

            if (!variables.TryGetValue("customerId", out var customerId) || string.IsNullOrWhiteSpace(customerId?.ToString()))
            {
                throw new InvalidOperationException("Missing or invalid 'customerId' variable.");
            }

            // Logge die Anfrage
            _logger.LogInformation("Issuing policy {PolicyId} for customer {CustomerId}", policyId, customerId);

            // Simuliere die Logik zum Ausstellen der Police
            await IssuePolicyAsync(policyId.ToString(), customerId.ToString(), ct);

            // Aktualisiere die Prozessvariablen
            variables["policyStatus"] = "Issued";

            // Logge den Abschluss
            _logger.LogInformation("Policy {PolicyId} successfully issued for customer {CustomerId}.", policyId, customerId);
        }

        private Task IssuePolicyAsync(string policyId, string customerId, CancellationToken ct)
        {
            // Simulierte Logik für das Ausstellen der Police
            // In einer echten Implementierung könnte dies ein API-Aufruf oder eine Datenbankoperation sein
            _logger.LogDebug("Simulating issuance of policy {PolicyId} for customer {CustomerId}", policyId, customerId);
            return Task.CompletedTask;
        }
    }
}