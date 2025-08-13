namespace VertexBPMN.Core.Services;

/// <summary>
/// Provides engine management and administrative operations.
/// </summary>
    public interface IManagementService
    {
        /// <summary>
        /// Suspends a process instance.
        /// </summary>
        ValueTask SuspendProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes a suspended process instance.
        /// </summary>
        ValueTask ResumeProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a process instance.
        /// </summary>
        ValueTask DeleteProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a job by ID (e.g., timer, async continuation).
        /// </summary>
        ValueTask ExecuteJobAsync(Guid jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets engine metrics (e.g., job counts, process instance counts).
        /// </summary>
        ValueTask<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken = default);
    }
