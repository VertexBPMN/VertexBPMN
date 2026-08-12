
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Interfaces
{
    public interface IBpmnParser
    {
        Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default);
        string Serialize(BpmnModel model);
    }
}

/// <summary>
/// Declares feature capability flags of a BPMN parser implementation (unified migration Phase 0).
/// All flags are additive, never subtractive; used by tooling & tests to shape expectations.
/// </summary>
public interface IBpmnParserCapabilities
{
    bool SupportsStrictRoundtrip { get; }
    bool SupportsRuntimeProjection { get; }
    bool SupportsCollaboration { get; }
    bool SupportsVendorNormalization { get; }
    bool SupportsAdvancedValidation { get; }
}

/// <summary>
/// Immutable capability snapshot. Extend only by adding new properties (backwards compatible).
/// </summary>
public readonly record struct BpmnParserCapabilities(
    bool SupportsStrictRoundtrip,
    bool SupportsRuntimeProjection,
    bool SupportsCollaboration,
    bool SupportsVendorNormalization,
    bool SupportsAdvancedValidation
) : IBpmnParserCapabilities;