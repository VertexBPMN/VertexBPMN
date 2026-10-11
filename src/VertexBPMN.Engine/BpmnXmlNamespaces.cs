using System.Diagnostics.CodeAnalysis;

namespace VertexBPMN.Engine;

[SuppressMessage("Security", "S5332", Justification = "These constants are XML namespace identifiers, never HTTP request targets. Their exact scheme is part of the BPMN/vendor vocabulary and must be preserved for parsing and roundtrip.")]
internal static class BpmnXmlNamespaces
{
	internal const string Camunda = "http://camunda.org/schema/1.0/bpmn";
	internal const string Zeebe = "http://zeebe.io/schema/zeebe/1.0";
	internal const string CamundaZeebe = "http://camunda.org/schema/zeebe/1.0";
	internal const string Flowable = "http://flowable.org/bpmn";
	internal const string Activiti = "http://activiti.org/bpmn";
	internal const string Cib = "http://cib.de/schema/bpmn";
	internal const string Jbpm = "http://jbpm.org/bpmn";
	internal const string Osmanthus = "http://osmanthus.io/bpmn";
	internal const string Alfresco = "http://alfresco.org/bpmn";
	internal const string Mcp = "http://vertexbpmn.io/mcp";
	internal const string Vertex = "http://vertexbpmn.io/schema/1.0";
	internal const string W4Graph = "http://www.w4.eu/spec/BPMN/20110930/GRAPH";
}
