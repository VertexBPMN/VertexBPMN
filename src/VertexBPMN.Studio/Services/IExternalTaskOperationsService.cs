namespace VertexBPMN.Studio.Services;

internal interface IExternalTaskOperationsService
{
    Task<IReadOnlyList<StudioExternalTaskProfile>> ListProfilesAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudioExternalTaskOperation>> ListForProcessAsync(
        string tenantId, Guid processInstanceId, string? activityId = null, CancellationToken cancellationToken = default);
}
