using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Resource role, as per Figure 10.7.
/// </summary>
public record ResourceRole(
    Resource ResourceRef,
    string? Name = null,
    List<ResourceParameterBinding>? ResourceParameterBindings = null,
    Expression? ResourceAssignmentExpression = null
) : BaseElement();