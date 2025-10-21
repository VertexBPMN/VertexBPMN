namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

#nullable enable

/// <summary>
/// Extension for mustUnderstand and definition, as per Figure 8.6.
/// </summary>
public record Extension(
    ExtensionDefinition Definition,
    bool MustUnderstand = false
);