namespace VertexBPMN.Domain.Interfaces;

/// <summary>Runs a bounded batch of accepted application endpoint starts.</summary>
public interface IApplicationEndpointReconciler
{
    Task ReconcileAcceptedAsync(CancellationToken cancellationToken = default);
}
