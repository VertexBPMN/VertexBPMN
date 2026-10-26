namespace VertexBPMN.Studio.Services;

/// <summary>
/// Studio client for the released Application API. It lets the low-code designer
/// list existing applications, open a draft, create a new application and save
/// (commit) a new content revision. All operations are tenant-scoped.
/// </summary>
internal interface IApplicationService
{
    Task<IReadOnlyList<StudioApplicationSummary>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<StudioApplicationDraft?> GetDraftAsync(string id, string tenantId, CancellationToken cancellationToken = default);
    Task<StudioApplicationSummary> CreateAsync(string tenantId, string key, string name, string content, CancellationToken cancellationToken = default);
    Task SaveDraftAsync(string id, string tenantId, string key, string name, int expectedVersion, string content, CancellationToken cancellationToken = default);
    Task PublishAsync(string id, string tenantId, int version, CancellationToken cancellationToken = default);
}
