using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Handlers
{
    /// <summary>
    /// Handler for informing the operations team about successful cancellations.
    /// </summary>
    public class InformOperationsSuccessfulCancelationHandler : IServiceTaskHandler
    {
        private readonly ILogger<InformOperationsSuccessfulCancelationHandler> _logger;

        public InformOperationsSuccessfulCancelationHandler(ILogger<InformOperationsSuccessfulCancelationHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the logic to inform the operations team about a successful cancellation.
        /// </summary>
        /// <param name="attributes">BPMN attributes for the task.</param>
        /// <param name="variables">Process variables that can be used or modified.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
        {
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));
            if (variables == null) throw new ArgumentNullException(nameof(variables));

            _logger.LogInformation("Starting task to inform operations team about successful cancellation...");

            // Extract required attributes
            if (!attributes.TryGetValue("operationsEmail", out var operationsEmail) || string.IsNullOrWhiteSpace(operationsEmail))
            {
                throw new InvalidOperationException("The 'operationsEmail' attribute is required but was not provided.");
            }

            // Extract optional variables
            variables.TryGetValue("cancelationDetails", out var cancelationDetails);

            // Simulate sending an email or notification
            _logger.LogInformation("Sending cancellation details to operations team at '{OperationsEmail}' with details: '{CancelationDetails}'.",
                operationsEmail, cancelationDetails);

            // Simulate async operation
            await Task.Delay(500, ct);

            // Log success
            _logger.LogInformation("Cancellation details successfully sent to operations team at '{OperationsEmail}'.", operationsEmail);

            // Optionally update process variables
            variables["operationsNotified"] = true;
            variables["operationsNotificationTimestamp"] = DateTime.UtcNow;

            _logger.LogInformation("Task to inform operations team about successful cancellation completed.");
        }
    }
}