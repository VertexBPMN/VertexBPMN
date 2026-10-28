namespace VertexBPMN.Api.Controllers;

/// <summary>Optional client idempotency key for a process-start request; duplicates reuse the first Accepted handle.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public MVC model-binding contract used by a public controller action.")]
public sealed class StartProcessIdempotencyRequest
{
    public string? IdempotencyKey { get; set; }
}
