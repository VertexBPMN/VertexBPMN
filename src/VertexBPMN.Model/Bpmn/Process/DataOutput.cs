using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Data output, as per Figure 10.61.
/// </summary>
public record DataOutput(
    string? Name = null,
    ItemDefinition? ItemSubjectRef = null,
    bool IsCollection = false
) : BaseElement(), ItemAwareElement;