using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Handlers
{
    /// <summary>
    /// Handler for informing customers about successful cancellations.
    /// </summary>
    public class InformCustomerSuccessfulCancelationHandler : IServiceTaskHandler
    {
        private readonly ILogger<InformCustomerSuccessfulCancelationHandler> _logger;

        public InformCustomerSuccessfulCancelationHandler(ILogger<InformCustomerSuccessfulCancelationHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the logic to inform the customer about a successful cancellation.
        /// </summary>
        /// <param name="attributes">BPMN attributes for the task.</param>
        /// <param name="variables">Process variables that can be used or modified.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
        {
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));
            if (variables == null) throw new ArgumentNullException(nameof(variables));

            _logger.LogInformation("Starting task to inform customer about successful cancellation...");

            // Extract required attributes
            if (!attributes.TryGetValue("customerEmail", out var customerEmail) || string.IsNullOrWhiteSpace(customerEmail))
            {
                throw new InvalidOperationException("The 'customerEmail' attribute is required but was not provided.");
            }

            // Extract optional variables
            variables.TryGetValue("cancelationReason", out var cancelationReason);

            // Simulate sending an email or notification
            _logger.LogInformation("Sending cancellation confirmation to '{CustomerEmail}' with reason: '{CancelationReason}'.",
                customerEmail, cancelationReason);

            // Simulate async operation
            await Task.Delay(500, ct);

            // Log success
            _logger.LogInformation("Cancellation confirmation successfully sent to '{CustomerEmail}'.", customerEmail);

            // Optionally update process variables
            variables["customerNotified"] = true;
            variables["notificationTimestamp"] = DateTime.UtcNow;

            _logger.LogInformation("Task to inform customer about successful cancellation completed.");
        }
    }
}