using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Handlers
{
    public class CancelApplicationServiceTaskHandler : IServiceTaskHandler
    {
        private readonly ILogger<CancelApplicationServiceTaskHandler> _logger;

        public CancelApplicationServiceTaskHandler(ILogger<CancelApplicationServiceTaskHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync(
            IDictionary<string, string> attributes,
            IDictionary<string, object> variables,
            CancellationToken ct = default)
        {
            // Extrahiere notwendige Variablen
            if (!variables.TryGetValue("applicationId", out var applicationId) || string.IsNullOrWhiteSpace(applicationId?.ToString()))
            {
                throw new InvalidOperationException("Missing or invalid 'applicationId' variable.");
            }

            if (!variables.TryGetValue("reason", out var reason) || string.IsNullOrWhiteSpace(reason?.ToString()))
            {
                throw new InvalidOperationException("Missing or invalid 'reason' variable.");
            }

            // Logge die Stornierungsanfrage
            _logger.LogInformation("Cancelling application {ApplicationId} for reason: {Reason}", applicationId, reason);

            // Simuliere die Stornierungslogik
            await CancelApplicationAsync(applicationId.ToString(), reason.ToString(), ct);

            // Aktualisiere die Prozessvariablen
            variables["applicationStatus"] = "Cancelled";

            // Logge den Abschluss
            _logger.LogInformation("Application {ApplicationId} successfully cancelled.", applicationId);
        }

        private Task CancelApplicationAsync(string applicationId, string reason, CancellationToken ct)
        {
            // Simulierte Logik für die Stornierung
            // In einer echten Implementierung könnte dies ein API-Aufruf oder eine Datenbankoperation sein
            _logger.LogDebug("Simulating cancellation of application {ApplicationId} with reason: {Reason}", applicationId, reason);
            return Task.CompletedTask;
        }
    }
}