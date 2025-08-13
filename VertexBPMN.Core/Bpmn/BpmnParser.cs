using System.Xml.Linq;

namespace VertexBPMN.Core.Bpmn;

/// <summary>
/// Einfache BPMN 2.0 XML-Parser-Basisklasse. Parst BPMN-Definitions und extrahiert Prozesselemente.
/// </summary>
public class BpmnParser
{
    public BpmnModel Parse(string bpmnXml)
    {
        var doc = XDocument.Parse(bpmnXml);
        var ns = doc.Root?.Name.Namespace ?? "";
        var process = doc.Descendants(ns + "process").FirstOrDefault();
        if (process == null) throw new InvalidOperationException("No <process> element found.");
        var id = (string?)process.Attribute("id") ?? "";
        var name = (string?)process.Attribute("name") ?? id;
        var eventNames = new[] { "startEvent", "endEvent", "boundaryEvent", "intermediateCatchEvent", "intermediateThrowEvent" };
        var events = process.Elements().Where(e => eventNames.Contains(e.Name.LocalName)).Select(e =>
        {
            var id = (string?)e.Attribute("id") ?? "";
            var type = e.Name.LocalName;
            var attachedTo = e.Name.LocalName == "boundaryEvent" ? (string?)e.Attribute("attachedToRef") : null;
            var cancelActivity = e.Name.LocalName == "boundaryEvent" ? (string?)e.Attribute("cancelActivity") != "false" : true;
            var isCompensation = false;
            string? eventDefinitionType = null;
            
            if (type == "boundaryEvent")
            {
                var comp = e.Elements().FirstOrDefault(x => x.Name.LocalName == "compensateEventDefinition");
                isCompensation = comp != null;
                
                // Detect event definition type for advanced boundary event handling
                var timerDef = e.Elements().FirstOrDefault(x => x.Name.LocalName == "timerEventDefinition");
                var messageDef = e.Elements().FirstOrDefault(x => x.Name.LocalName == "messageEventDefinition");
                var errorDef = e.Elements().FirstOrDefault(x => x.Name.LocalName == "errorEventDefinition");
                var signalDef = e.Elements().FirstOrDefault(x => x.Name.LocalName == "signalEventDefinition");
                
                if (timerDef != null) eventDefinitionType = "timer";
                else if (messageDef != null) eventDefinitionType = "message";
                else if (errorDef != null) eventDefinitionType = "error";
                else if (signalDef != null) eventDefinitionType = "signal";
                else if (comp != null) eventDefinitionType = "compensation";
            }
            
            return new BpmnEvent(id, type, attachedTo, isCompensation, cancelActivity, eventDefinitionType);
        }).ToList();
        // Event Subprocesses
    var tasks = process.Elements().Where(e => e.Name.LocalName.EndsWith("Task") || e.Name.LocalName == "callActivity").Select(e => new BpmnTask((string?)e.Attribute("id") ?? "", e.Name.LocalName)).ToList();
        var gateways = process.Elements().Where(e => e.Name.LocalName.EndsWith("Gateway")).Select(e =>
        {
            var type = e.Name.LocalName;
            var idAttr = (string?)e.Attribute("id") ?? "";
            return new BpmnGateway(idAttr, type);
        }).ToList();
        var subprocesses = process.Elements().Where(e => e.Name.LocalName == "subProcess" || e.Name.LocalName == "adHocSubProcess").Select(e =>
        {
            var idAttr = (string?)e.Attribute("id") ?? "";
            var mi = e.Elements().FirstOrDefault(x => x.Name.LocalName == "multiInstanceLoopCharacteristics");
            var isMultiInstance = mi != null;
            var isSequential = mi?.Attribute("isSequential")?.Value == "true";
            var loopCardinality = mi?.Element(XName.Get("loopCardinality", e.Name.NamespaceName))?.Value;
            var isEventSubprocess = (string?)e.Attribute("triggeredByEvent") == "true";
            var isTransaction = (string?)e.Attribute("transaction") == "true";
            
            int? parsedCardinality = null;
            if (int.TryParse(loopCardinality, out var card))
                parsedCardinality = card;
                
            return new BpmnSubprocess(idAttr, isMultiInstance, isEventSubprocess, isTransaction, isSequential, parsedCardinality);
        }).ToList();
        var sequenceFlows = process.Elements().Where(e => e.Name.LocalName == "sequenceFlow").Select(e => new BpmnSequenceFlow((string?)e.Attribute("id") ?? "", (string?)e.Attribute("sourceRef") ?? "", (string?)e.Attribute("targetRef") ?? "")).ToList();
        return new BpmnModel(id, name, events, tasks, gateways, subprocesses, sequenceFlows);
    }
    public string Serialize(BpmnModel model)
    {
        var ns = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        var doc = new XDocument(
            new XElement(XName.Get("definitions", ns),
                new XElement(XName.Get("process", ns),
                    new XAttribute("id", model.Id),
                    new XAttribute("name", model.Name),
                    model.Events.Select(e =>
                        new XElement(XName.Get(e.Type, ns),
                            new XAttribute("id", e.Id),
                            e.AttachedToRef != null ? new XAttribute("attachedToRef", e.AttachedToRef) : null
                        )
                    ),
                    model.Tasks.Select(t =>
                        new XElement(XName.Get(t.Type, ns),
                            new XAttribute("id", t.Id)
                        )
                    ),
                    model.Gateways.Select(g =>
                        new XElement(XName.Get(g.Type, ns),
                            new XAttribute("id", g.Id)
                        )
                    ),
                    model.Subprocesses.Select(s =>
                        new XElement(XName.Get("subProcess", ns),
                            new XAttribute("id", s.Id),
                            s.IsMultiInstance ? new XElement(XName.Get("multiInstanceLoopCharacteristics", ns)) : null,
                            s.IsEventSubprocess ? new XAttribute("triggeredByEvent", "true") : null,
                            s.IsTransaction ? new XAttribute("transaction", "true") : null
                        )
                    ),
                    model.SequenceFlows.Select(f =>
                        new XElement(XName.Get("sequenceFlow", ns),
                            new XAttribute("id", f.Id),
                            new XAttribute("sourceRef", f.SourceRef),
                            new XAttribute("targetRef", f.TargetRef)
                        )
                    )
                )
            )
        );
        return doc.ToString();
    }
}

public partial record BpmnModel(
    string Id,
    string Name,
    List<BpmnEvent> Events,
    List<BpmnTask> Tasks,
    List<BpmnGateway> Gateways,
    List<BpmnSubprocess> Subprocesses,
    List<BpmnSequenceFlow> SequenceFlows
);

public partial record BpmnModel
{
    public string ProcessId => Id;
    public IEnumerable<object> Activities => Tasks.Cast<object>().Concat(Subprocesses);
}
public record BpmnSubprocess(string Id, bool IsMultiInstance, bool IsEventSubprocess = false, bool IsTransaction = false, bool IsSequential = false, int? LoopCardinality = null);

public record BpmnEvent(string Id, string Type, string? AttachedToRef = null, bool IsCompensation = false, bool CancelActivity = true, string? EventDefinitionType = null);
public record BpmnTask(string Id, string Type);
public record BpmnGateway(string Id, string Type);
public record BpmnSequenceFlow(string Id, string SourceRef, string TargetRef);
