namespace VertexBPMN.Parsing;

/// <summary>
/// Describes the capabilities of a BPMN parser implementation.
/// Used to indicate which advanced features are supported.
/// </summary>
/// <param name="SupportsStrictRoundtrip">Parser supports lossless XML roundtrip preservation</param>
/// <param name="SupportsRuntimeProjection">Parser can build lightweight runtime models</param>
/// <param name="SupportsCollaboration">Parser handles collaboration elements (participants, messageFlow)</param>
/// <param name="SupportsVendorNormalization">Parser normalizes vendor extensions into structured form</param>
/// <param name="SupportsAdvancedValidation">Parser provides comprehensive structured validation diagnostics</param>
public readonly record struct BpmnParserCapabilities(
    bool SupportsStrictRoundtrip,
    bool SupportsRuntimeProjection,
    bool SupportsCollaboration,
    bool SupportsVendorNormalization,
    bool SupportsAdvancedValidation
);