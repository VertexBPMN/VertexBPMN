using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

#nullable enable

/// <summary>
/// Extension elements for any XML elements, as per Figure 8.6.
/// </summary>
public record ExtensionElements(
    List<object> Any
);