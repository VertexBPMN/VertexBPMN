using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

/// <summary>
/// Output data item stub.
/// </summary>
public record OutputDataItem(string Name) : BaseElement, ItemAwareElement;