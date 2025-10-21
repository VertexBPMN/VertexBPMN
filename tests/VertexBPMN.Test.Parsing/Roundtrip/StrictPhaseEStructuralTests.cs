using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhaseEStructuralTests
{
    private static readonly BpmnParser P = new(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict, PreserveUnknownExtensions = true });
    private static readonly XNamespace BPMN = "http://www.omg.org/spec/BPMN/20100524/MODEL";

    private static void StructuralCompare(XElement expected, XElement actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        var expAttrs = expected.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a=>a.Name.ToString()).ToList();
        var actAttrs = actual.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a=>a.Name.ToString()).ToList();
        Assert.Equal(expAttrs.Count, actAttrs.Count);
        for(int i=0;i<expAttrs.Count;i++)
        {
            Assert.Equal(expAttrs[i].Name, actAttrs[i].Name);
            Assert.Equal(expAttrs[i].Value, actAttrs[i].Value);
        }
        var expChildren = expected.Elements().ToList();
        var actChildren = actual.Elements().ToList();
        Assert.Equal(expChildren.Count, actChildren.Count);
        for(int i=0;i<expChildren.Count;i++) StructuralCompare(expChildren[i], actChildren[i]);
    }

//    [Fact]
//    public void Structural_Gateway_ForkJoin_Roundtrip()
//    {
//        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
//  <bpmn:process id='p1'>
//    <bpmn:startEvent id='start'/>
//    <bpmn:parallelGateway id='gw_split'/>
//    <bpmn:task id='t1' name='A'/>
//    <bpmn:task id='t2' name='B'/>
//    <bpmn:parallelGateway id='gw_join'/>
//    <bpmn:endEvent id='end'/>
//    <bpmn:sequenceFlow id='f_start_split' sourceRef='start' targetRef='gw_split'/>
//    <bpmn:sequenceFlow id='f_split_t1' sourceRef='gw_split' targetRef='t1'/>
//    <bpmn:sequenceFlow id='f_split_t2' sourceRef='gw_split' targetRef='t2'/>
//    <bpmn:sequenceFlow id='f_t1_join' sourceRef='t1' targetRef='gw_join'/>
//    <bpmn:sequenceFlow id='f_t2_join' sourceRef='t2' targetRef='gw_join'/>
//    <bpmn:sequenceFlow id='f_join_end' sourceRef='gw_join' targetRef='end'/>
//  </bpmn:process>
//</bpmn:definitions>";
//        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
//        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
//        var exp = XDocument.Parse(xml).Root!;
//        var act = XDocument.Parse(outXml).Root!;
//        // Compare only the process subtree for now (namespace attr ordering may differ at root)
//        var expProc = exp.Element(BPMN + "process")!;
//        var actProc = act.Element(BPMN + "process")!;
//        StructuralCompare(expProc, actProc);
    //}

//    [Fact]
//    public void Structural_Unknown_EventDefinition_Preserved()
//    {
//        const string xml = @"<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:ven='http://vendor/x'>
//  <bpmn:process id='p4'>
//    <bpmn:startEvent id='s'>
//      <bpmn:extensionElements><ven:meta k='v'/></bpmn:extensionElements>
//      <ven:customEventDefinition foo='bar'/>
//    </bpmn:startEvent>
//  </bpmn:process>
//</bpmn:definitions>";
//        var model = P.ParseAsync(xml).GetAwaiter().GetResult();
//        var outXml = new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);
//        var exp = XDocument.Parse(xml).Root!;
//        var act = XDocument.Parse(outXml).Root!;
//        var expEvt = exp.Descendants(BPMN + "startEvent").First();
//        var actEvt = act.Descendants(BPMN + "startEvent").First();
//        StructuralCompare(expEvt, actEvt);
//    }
}
