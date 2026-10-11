using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser
{
	private sealed class DefinitionCapture(bool strict)
	{
		internal Dictionary<string, string>? RawDefinitionsAttr { get; set; } = strict ? new(StringComparer.Ordinal) : null;
		internal Dictionary<string, string>? RawProcessAttr { get; set; } = strict ? new(StringComparer.Ordinal) : null;
		internal Dictionary<string, List<string>>? RawIncoming { get; set; } = strict ? new(StringComparer.Ordinal) : null;
		internal Dictionary<string, List<string>>? RawOutgoing { get; set; } = strict ? new(StringComparer.Ordinal) : null;
		internal List<NamespacePrefix>? NamespacePrefixes { get; set; } = strict ? [] : null;
		internal List<XElement>? RawGlobalElements { get; set; } = strict ? [] : null;
		internal XElement? DiRoot { get; set; }
	}
}

