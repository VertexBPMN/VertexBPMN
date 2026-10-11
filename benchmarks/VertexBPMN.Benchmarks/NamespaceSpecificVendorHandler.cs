using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model;

namespace VertexBPMN.Benchmarks;

internal sealed class NamespaceSpecificVendorHandler : IBpmnVendorExtensionInterpreter
{
	private readonly string _targetNamespace;

	public NamespaceSpecificVendorHandler(string targetNamespace)
	{
		_targetNamespace = targetNamespace;
	}

	public bool WasInvoked { get; private set; }
	public string? ProcessedNamespace { get; private set; }

	public string[] SupportedNamespaces => new[] { _targetNamespace };

	public bool CanHandle(string namespaceUri, string localName)
	{
		return namespaceUri == _targetNamespace;
	}

	public VendorExtensionResult ProcessExtension(XElement element, string ownerElementId)
	{
		WasInvoked = true;
		ProcessedNamespace = element.Name.NamespaceName;

		return new VendorExtensionResult
		{
			NormalizedAttributes = { [$"{element.Name.LocalName}.processed"] = "true" }
		};
	}
}
