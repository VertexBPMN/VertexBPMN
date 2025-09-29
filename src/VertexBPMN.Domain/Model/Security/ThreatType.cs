namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Types of security threats that can be detected.
/// </summary>
public enum ThreatType
{
    MaliciousContent,
    SuspiciousNamespace,
    ExcessiveCDATA,
    BinaryContent,
    EntityExpansion,
    ResourceExhaustion
}