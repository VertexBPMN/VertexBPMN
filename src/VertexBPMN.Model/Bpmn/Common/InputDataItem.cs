using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

/// <summary>
/// Input data item stub.
/// </summary>
public record InputDataItem(string Name) : BaseElement, ItemAwareElement;