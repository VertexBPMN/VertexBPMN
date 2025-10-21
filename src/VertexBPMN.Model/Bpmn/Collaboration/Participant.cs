using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Service;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Participant, as per Figure 9.7.
/// </summary>
public record Participant(
    string Name,
    Process.Process? ProcessRef = null,
    List<Interface> InterfaceRefs = null!,
    List<EndPoint> EndPointRefs = null!,
    ParticipantMultiplicity? ParticipantMultiplicity = null,
    PartnerRole? PartnerRoleRef = null,
    PartnerEntity? PartnerEntityRef = null
) : InteractionNode;