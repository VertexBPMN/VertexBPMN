using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Data store, as per Figure 10.55.
/// </summary>
public record DataStore(
    string Name,
    int? Capacity = null,
    bool IsUnlimited = true,
    ItemDefinition? ItemSubjectRef = null
) : RootElement, ItemAwareElement;