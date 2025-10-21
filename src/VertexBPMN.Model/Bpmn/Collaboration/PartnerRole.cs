using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Partner role, as per the specification.
/// </summary>
public record PartnerRole(
    string Name,
    List<ItemDefinition> TypeRef = null!
) : RootElement;