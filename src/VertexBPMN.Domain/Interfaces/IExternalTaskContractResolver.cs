using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Server-side policy boundary. Implementations authorize tenant/topic/profile and validate mapped inputs,
/// including rejecting secret references. Called in a runtime transaction: use local policy snapshots,
/// never network I/O. Registration is restricted to the explicitly enabled local scheduling preview.
/// </summary>
public interface IExternalTaskContractResolver
{
	ValueTask ValidateDeploymentAsync(string tenantId, ExternalTaskDefinition definition,
		IReadOnlyCollection<string> inputNames, CancellationToken cancellationToken = default);

	ValueTask<ResolvedExternalTaskContract> ResolveAsync(string tenantId, ExternalTaskDefinition definition,
		IReadOnlyDictionary<string, object> inputs, CancellationToken cancellationToken = default);
}
