using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

#nullable enable

/// <summary>
/// Abstract superclass for all CMMN elements (Figure 5.1).
/// Extension: Added optional runtime hooks (e.g., lifecycle events).
/// </summary>
public abstract record CMMNElement(
    string? Id = null,
    string? Name = null,
    List<Documentation>? Documentations = null,
    List<ExtensionDefinition>? ExtensionDefinitions = null,
    List<ExtensionAttributeValue>? ExtensionValues = null,
    List<RuntimeHook>? RuntimeHooks = null // Extension: Custom lifecycle callbacks.
);