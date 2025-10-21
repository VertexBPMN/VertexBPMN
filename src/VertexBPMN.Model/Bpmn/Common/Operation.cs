using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Operation class, as per Figure 8.36.
/// </summary>
public record Operation(
    string Name,
    Message InMessageRef,
    Message? OutMessageRef = null,
    List<Error> ErrorRefs = null!
) : BaseElement()
{
    public string? ImplementationRef { get; set; }
}