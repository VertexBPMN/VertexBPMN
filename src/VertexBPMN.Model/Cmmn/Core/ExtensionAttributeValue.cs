namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Extension attribute value.
/// </summary>
public record ExtensionAttributeValue(
    object? Value = null,
    object? ValueRef = null
) : CMMNElement();