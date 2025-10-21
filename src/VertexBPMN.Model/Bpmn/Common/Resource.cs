using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Resource class, as per Figure 8.31.
/// </summary>
public record Resource(
    string Name,
    List<ResourceParameter> ResourceParameters = null!
) : RootElement();