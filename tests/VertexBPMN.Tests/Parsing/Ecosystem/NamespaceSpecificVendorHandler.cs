using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Ecosystem;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Ecosystem;

/// <summary>
/// Test implementation of namespace-specific vendor handler.
/// </summary>
internal sealed class NamespaceSpecificVendorHandler(string targetNamespace) : IBpmnVendorExtensionInterpreter
{
	private readonly string _targetNamespace = targetNamespace;

	public bool WasInvoked { get; private set; }
	public string? ProcessedNamespace { get; private set; }

	public string[] SupportedNamespaces => [_targetNamespace];

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
