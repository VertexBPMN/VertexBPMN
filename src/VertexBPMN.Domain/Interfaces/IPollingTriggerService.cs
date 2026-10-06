namespace VertexBPMN.Domain.Interfaces;

public interface IPollingTriggerService
{
	Task<IReadOnlyList<PollingTriggerInfo>> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default);
	Task<PollingTriggerInfo?> GetAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
	Task<PollingTriggerCreated> CreateAsync(PollingTriggerWriteRequest request, string? tenantId = null, CancellationToken cancellationToken = default);
	Task<bool> UpdateAsync(Guid id, PollingTriggerWriteRequest? request = null, bool? enabled = null, string? tenantId = null, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
	/// <summary>Polls a trigger immediately (bypassing the interval) — used to test without waiting for the scheduler tick.</summary>
	Task<PollingTriggerInfo?> PollNowAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
}
