using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Handlers
{
    /// <summary>
    /// Handler for reporting potential fraud cases.
    /// </summary>
    public class ReportFraudHandler : IServiceTaskHandler
    {
        private readonly ILogger<ReportFraudHandler> _logger;

        public ReportFraudHandler(ILogger<ReportFraudHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the fraud reporting logic.
        /// </summary>
        /// <param name="attributes">BPMN attributes for the task.</param>
        /// <param name="variables">Process variables that can be used or modified.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
        {
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));
            if (variables == null) throw new ArgumentNullException(nameof(variables));

            _logger.LogInformation("Starting fraud report task...");

            // Extract required attributes
            if (!attributes.TryGetValue("fraudType", out var fraudType) || string.IsNullOrWhiteSpace(fraudType))
            {
                throw new InvalidOperationException("The 'fraudType' attribute is required but was not provided.");
            }

            if (!attributes.TryGetValue("reportingServiceUrl", out var reportingServiceUrl) || string.IsNullOrWhiteSpace(reportingServiceUrl))
            {
                throw new InvalidOperationException("The 'reportingServiceUrl' attribute is required but was not provided.");
            }

            // Extract optional variables
            variables.TryGetValue("customerId", out var customerId);
            variables.TryGetValue("transactionId", out var transactionId);

            // Simulate reporting fraud (e.g., sending an HTTP request)
            _logger.LogInformation("Reporting fraud of type '{FraudType}' for customer '{CustomerId}' and transaction '{TransactionId}' to '{ReportingServiceUrl}'.",
                fraudType, customerId, transactionId, reportingServiceUrl);

            // Simulate async operation
            await Task.Delay(500, ct);

            // Log success
            _logger.LogInformation("Fraud report successfully sent to '{ReportingServiceUrl}'.", reportingServiceUrl);

            // Optionally update process variables
            variables["fraudReported"] = true;
            variables["fraudReportTimestamp"] = DateTime.UtcNow;

            _logger.LogInformation("Fraud report task completed.");
        }
    }
}