//See docs/ROUNDTRIP_STRICT_PLAN.md
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Serialization;

public class BpmnSerializer
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly Dictionary<string,string> WellKnownPrefixes = new()
    {
        {"http://camunda.org/schema/1.0/bpmn","camunda"},
        {"http://zeebe.io/schema/zeebe/1.0","zeebe"},
        {"http://vertexbpmn.io/schema/1.0","vertex"},
        {"http://vertexbpmn.io/schema/1.0/bpmn","vertex"},
        {"https://vertexbpmn.io/schema/bpmn/1.0","vertex"}
    };
