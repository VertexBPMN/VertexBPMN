using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Global task, as per Figure 10.44.
/// </summary>
public record GlobalTask(
    List<ResourceRole> Performers = null!
) : CallableElement;