namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Stable public codes; exception messages/stdout/stderr must not be returned to clients.</summary>
public enum SourceControlErrorCode
{
    Unauthenticated, Forbidden, NotFound, RevisionConflict, IdempotencyConflict,
    InvalidInput, PayloadTooLarge, QuotaExceeded, Disabled, GitUnavailable,
    ProviderUnavailable, CredentialUnavailable, ReleaseNotApproved,
    ContentUnsafe, TimedOut, Cancelled, ResultUnknown
}
