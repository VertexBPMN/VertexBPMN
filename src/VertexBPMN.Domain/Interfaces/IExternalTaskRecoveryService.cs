using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public interface IExternalTaskRecoveryService
{
	ValueTask<ExternalTaskRecoveryResult> RecoverAsync(
		int maximumJobs = 100,
		CancellationToken cancellationToken = default);
}
