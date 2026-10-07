// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public enum GatewayDecisionKind
{
	Selected,
	DefaultSelected,
	NoOutgoingFlow
}
