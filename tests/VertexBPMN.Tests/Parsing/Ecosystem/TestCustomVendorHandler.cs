using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Ecosystem;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Ecosystem;

/// <summary>
/// Test implementation of custom vendor handler.
/// </summary>
internal sealed class TestCustomVendorHandler : IBpmnVendorExtensionInterpreter
{
	public bool WasInvoked { get; private set; }
	public List<string> ProcessedElements { get; } = [];

	public string[] SupportedNamespaces => ["http://test.vendor/extensions"];

	public bool CanHandle(string namespaceUri, string localName)
	{
		return namespaceUri == "http://test.vendor/extensions";
	}

	public VendorExtensionResult ProcessExtension(XElement element, string ownerElementId)
	{
		WasInvoked = true;
		ProcessedElements.Add($"{element.Name.LocalName}");

		var result = new VendorExtensionResult();

		if (element.Name.LocalName == "specialProcessor")
		{
			result.NormalizedAttributes["custom:processor.type"] = element.Attribute("type")?.Value ?? "";
		}
		else if (element.Name.LocalName == "config")
		{
			foreach (var attr in element.Attributes())
			{
				result.NormalizedAttributes[$"custom:config.{attr.Name.LocalName}"] = attr.Value;
			}
		}

		return result;
	}
}
