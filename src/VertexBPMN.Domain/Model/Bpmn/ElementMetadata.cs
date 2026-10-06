// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Per-element metadata captured for strict ordering & attribute preservation.
/// </summary>
public sealed record ElementMetadata(
	int OrderIndex,
	string ElementName,
	IReadOnlyDictionary<string, string> Attributes,
	bool HadCamundaCollection = false,
	bool HadZeebeInputCollection = false,
	bool HadLoopCardinality = false,
	bool HadCamundaElementVar = false,
	bool HadZeebeInputElement = false,
	bool HadZeebeOutputElement = false
);
