namespace VertexBPMN.Portal.Bff;

/// <summary>
/// The portal BFF's server-side gateway to the Vertex REST API. The BFF derives the
/// actor, tenant and released revision itself and hands only resolved values to the
/// gateway; the browser never supplies tenant/assignee/revision. Implementations
/// forward using the user's server-held OIDC access token.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public gateway interface required for the public BFF controller's dependency injection surface.")]
public interface IBffRuntimeGateway
{
    /// <summary>Resolves ONLY the released revision (never the latest/draft/) of an application.</summary>
    Task<ReleasedApplication?> GetReleasedAsync(string tenantId, string applicationId, CancellationToken ct = default);

    /// <summary>Executes a data-source read that was already validated by the narrow resolver.
    /// <paramref name="bindingName"/> is the server-resolved binding name; never a raw query.
    /// </summary>
    Task<DataSourceResult> ExecuteDataSourceReadAsync(string tenantId, string applicationId, string bindingName, CancellationToken ct = default);

    /// <summary>
    /// Starts the released process referenced by the released start action, for the derived
    /// actor/tenant. The released <paramref name="processDefinitionId"/> is server-resolved from
    /// the released revision; never caller-supplied. State-changing; forwarded with the idempotency key.
    /// </summary>
    Task StartProcessAsync(string tenantId, string processDefinitionId, IDictionary<string, object?>? variables, string idempotencyKey, CancellationToken ct = default);

    /// <summary>Claims a task for the derived actor.</summary>
    Task ClaimAsync(string tenantId, string applicationId, Guid taskId, string idempotencyKey, CancellationToken ct = default);

    /// <summary>Completes a task for the derived actor.</summary>
    Task CompleteAsync(string tenantId, string applicationId, Guid taskId, string idempotencyKey, IReadOnlyDictionary<string, object?>? variables, CancellationToken ct = default);

    /// <summary>Reads a task's raw lifecycle state for the derived actor (projection happens in the controller).</summary>
    Task<BffTaskState> GetTaskStateAsync(string tenantId, string applicationId, Guid taskId, CancellationToken ct = default);
}
