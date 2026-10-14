using System.Text.Json.Serialization;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// A single property of a connector template (shown in the Studio palette and, after
/// flattening, consumed by the unified executor pipeline).
/// </summary>
/// <param name="Key">Canonical attribute key, e.g. <c>vertex:connector.method</c> or a parameter name.</param>
/// <param name="Type">Schema-ish type hint: <c>string</c>, <c>number</c>, <c>boolean</c>, <c>object</c>, <c>array</c>.</param>
/// <param name="Required">Whether the property must be provided before the template is usable.</param>
/// <param name="DefaultValue">Serialized default as literal (no variable reference).</param>
/// <param name="Options">Closed set of allowed values, if any.</param>
/// <param name="BindingVersion">Binding contract version this property conforms to.
/// 1 = legacy flat attributes (<c>vertex:connector.*</c> text mapping); 2 = versioned binding
/// metadata with explicit runtime-config/input/output/credential distinction (see
/// <c>ConnectorBindingVersion</c> and the low-code application contracts document).</param>
public sealed record ConnectorTemplateProperty(
	string Key,
	string Type,
	bool Required = false,
	[property: JsonPropertyName("default")] string? DefaultValue = null,
	IReadOnlyList<string>? Options = null,
	int BindingVersion = ConnectorBindingVersion.Legacy);
