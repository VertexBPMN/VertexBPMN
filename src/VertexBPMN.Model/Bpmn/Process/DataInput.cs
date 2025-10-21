using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Data input, as per Figure 10.59.
/// </summary>
public record DataInput(
    string? Name = null,
    ItemDefinition? ItemSubjectRef = null,
    bool IsCollection = false
) : BaseElement(), ItemAwareElement;