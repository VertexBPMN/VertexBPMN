using System.Xml.Linq;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public sealed class CamundaNormalizedFormFieldTests
{
	[Fact]
	public void NormalizedFormFieldsUseFieldIdAndPropertyWithoutOutOfRangeAccess()
	{
		XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
		XNamespace camunda = "http://camunda.org/schema/1.0/bpmn";
		var task = new XElement(bpmn + "userTask", new XAttribute("id", "approval"));
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["camunda:formField.approval.type"] = "boolean",
			["camunda:formField.approval.label"] = "Approved",
			["camunda:formField.incomplete"] = "ignored"
		};
		var namespaces = new SortedDictionary<string, string>(StringComparer.Ordinal)
		{
			["camunda"] = camunda.NamespaceName
		};
		var serializer = new NormalizedProjectionSerializer();

		serializer.AddVendorExtensions(task, attributes, namespaces);

		var formData = Assert.Single(task.Descendants(camunda + "formData"));
		var field = Assert.Single(formData.Elements(camunda + "formField"));
		Assert.Equal("approval", (string?)field.Attribute("id"));
		Assert.Equal("boolean", (string?)field.Attribute("type"));
		Assert.Equal("Approved", (string?)field.Attribute("label"));
	}
}
