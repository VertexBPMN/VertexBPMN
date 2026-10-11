using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model;

namespace VertexBPMN.Benchmarks;

internal sealed class TestCustomVendorHandler : IBpmnVendorExtensionInterpreter
{
	public bool WasInvoked { get; private set; }
	public List<string> ProcessedElements { get; } = new();

	private const string VendorNamespace = "http://test.vendor/extensions";
	public string[] SupportedNamespaces => [VendorNamespace];

	public bool CanHandle(string namespaceUri, string localName)
	{
		return namespaceUri == VendorNamespace;
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
