using System.Xml.Linq;

namespace VertexBPMN.Domain.Model;

public static class Ns
{
    public static readonly XNamespace BPMN = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    public static readonly XNamespace BPMNDI = "http://www.omg.org/spec/BPMN/20100524/DI";
    public static readonly XNamespace DI = "http://www.omg.org/spec/DD/20100524/DI";
    public static readonly XNamespace DC = "http://www.omg.org/spec/DD/20100524/DC";
    public static readonly XNamespace XSI = "http://www.w3.org/2001/XMLSchema-instance";
    public static readonly XNamespace DMN = "https://www.omg.org/spec/DMN/20191111/MODEL/";
    public static readonly XNamespace DMNDI = "https://www.omg.org/spec/DMN/20191111/DMNDI/";
    public static readonly XNamespace BPMNIO = "http://bpmn.io/schema/bpmn";
    public static readonly XNamespace BPMNE =  "http://www.omg.org/spec/BPMN/20100524/MODEL";
}