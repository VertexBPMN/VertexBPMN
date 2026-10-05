using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Public-safe failure: never includes untrusted input or provider diagnostics.</summary>
public sealed class SourceControlSecurityException(SourceControlErrorCode code)
    : Exception($"Source control request rejected ({code}).")
{
    public SourceControlErrorCode Code { get; } = code;
}
