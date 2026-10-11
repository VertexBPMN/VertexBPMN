using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

/// <summary>
/// Performs only the long-running external work. Implementations receive no API client,
/// so the host remains the sole owner of lease mutation and completion.
/// </summary>
internal interface IExternalTaskHandler
{
    string Topic { get; }
    ValueTask<ExternalTaskHandlerResult> HandleAsync(ExternalTaskLease lease, CancellationToken cancellationToken);
}
