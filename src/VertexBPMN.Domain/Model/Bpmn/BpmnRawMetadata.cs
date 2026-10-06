// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Raw metadata captured for strict roundtrip mode (Phase 1/2 + Phase A extensions).
/// RoundtripDirty indicates the model was mutated after parsing and a lossless emit may not be valid.
/// </summary>
public sealed record BpmnRawMetadata(
	IReadOnlyDictionary<string, string>? DefinitionsAttributes = null,
	IReadOnlyDictionary<string, string>? ProcessAttributes = null,
	IReadOnlyDictionary<string, IReadOnlyList<string>>? Incoming = null,
	IReadOnlyDictionary<string, IReadOnlyList<string>>? Outgoing = null,
	IReadOnlyDictionary<string, (string Raw, bool WasCData)>? SequenceFlowConditions = null,
	IReadOnlyDictionary<string, XElement>? RawExtensionElements = null,
	IReadOnlyDictionary<string, IReadOnlyList<XElement>>? RawEventDefinitions = null,
	IReadOnlyDictionary<string, XElement>? RawMultiInstance = null,
	IReadOnlyDictionary<string, string>? PriorityAttributeNamespace = null,
	IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? FlowNodeAttributes = null,
	bool RoundtripDirty = false,
	IReadOnlyList<NamespacePrefix>? NamespacePrefixes = null,
	IReadOnlyDictionary<string, ElementMetadata>? ElementsMetadata = null,
	IReadOnlyList<XElement>? RawGlobalElements = null,
	IReadOnlyList<XElement>? RawArtifacts = null,
	IReadOnlyList<XElement>? RawLanes = null,
	IReadOnlyDictionary<string, IReadOnlyList<XElement>>? RawDocumentation = null,
	XElement? RawDiRoot = null,
	IReadOnlySet<string>? PartiallyDirtyElements = null,
	IReadOnlyDictionary<string, string>? GlobalElementKinds = null,
	IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? VendorNormalizedExtensions = null,
	string? OriginalXml = null
);
