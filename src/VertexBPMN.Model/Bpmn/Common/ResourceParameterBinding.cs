using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Resource parameter binding, as per Figure 8.31.
/// </summary>
public record ResourceParameterBinding(
    ResourceParameter ParameterRef,
    Expression Expression
) : BaseElement();