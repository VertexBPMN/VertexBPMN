namespace VertexBPMN.SourceControl.Abstractions;
public sealed record SourceControlAvailability(bool Available, SourceControlCapability Capabilities,
    SourceControlErrorCode? UnavailableReason);
