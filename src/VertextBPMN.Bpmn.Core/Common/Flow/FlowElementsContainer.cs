using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Services;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Flow;

public class FlowElementsContainer : CallableElement
{
    public IReadOnlyList<FlowElement> FlowElements { get; } = [];
}