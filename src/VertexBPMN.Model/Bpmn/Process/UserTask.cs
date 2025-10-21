using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// User task, as per Figure 10.22.
/// </summary>
public record UserTask(
    string? Implementation = null,
    List<Rendering> Renderings = null!
) : Task;