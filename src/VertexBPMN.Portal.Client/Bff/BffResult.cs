using System.Diagnostics.CodeAnalysis;

namespace VertexBPMN.Portal.Client.Bff;

/// <summary>
/// Typed result of a Portal BFF call. On failure it carries the BFF problem-detail error code
/// (e.g. <c>bffUnsanctionedBinding</c>, <c>bffMissingIdempotencyKey</c>) plus the HTTP status so the
/// caller can react to server-side policy without parsing JSON.
/// </summary>
[SuppressMessage("Design", "CA1000", Justification = "Static factory members on the generic BFF result are the idiomatic construction surface for the client.")]
public sealed record BffResult<T>(bool IsSuccess, T? Value, string? ErrorCode, int? StatusCode, string? Detail)
{
    public static BffResult<T> Success(T value) => new(true, value, null, null, null);

    public static BffResult<T> Failure(string errorCode, int statusCode, string? detail)
        => new(false, default, errorCode, statusCode, detail);
}

/// <summary>Non-generic BFF result for NoContent state-changing calls.</summary>
public sealed record BffResult(bool IsSuccess, string? ErrorCode, int? StatusCode, string? Detail)
{
    public static BffResult Success() => new(true, null, null, null);

    public static BffResult Failure(string errorCode, int statusCode, string? detail)
        => new(false, errorCode, statusCode, detail);
}
