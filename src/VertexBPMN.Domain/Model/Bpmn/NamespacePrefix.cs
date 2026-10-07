// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Namespace prefix entry for strict roundtrip (Phase A).
/// </summary>
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record NamespacePrefix(string Prefix, string Uri, bool Original = true)
{
	public NamespacePrefix(string prefix, System.Uri uri, bool original = true)
		: this(prefix, uri?.OriginalString ?? throw new ArgumentNullException(nameof(uri)), original) { }
}
