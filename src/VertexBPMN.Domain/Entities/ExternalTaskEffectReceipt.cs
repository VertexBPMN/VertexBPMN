namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Server-side, persisted witness that a business effect was registered exactly once for an
/// external-task activity execution. The stable <see cref="EffectKey"/> is bound to the
/// activity execution (lease-generation independent), so retries, response loss and reused
/// completions cannot create a second observable effect row, while lease loss records none.
/// </summary>
public sealed class ExternalTaskEffectReceipt
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ActivityExecutionId { get; set; }
    public string EffectKey { get; set; } = string.Empty;
    public string EffectName { get; set; } = string.Empty;
    public Guid CompletionId { get; set; }
    public string ResultHash { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
}
