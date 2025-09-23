using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Parsing;

public class BpmnSerializer
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly Dictionary<string,string> WellKnownPrefixes = new()
    {
        {"http://camunda.org/schema/1.0/bpmn","camunda"},
        {"http://zeebe.io/schema/zeebe/1.0","zeebe"},
        {"http://vertexbpmn.io/schema/1.0","vertex"}
    };

    public string Serialize(BpmnModel model)
    {
        // Collect vendor namespaces from extension attributes and loop characteristics / flow priorities
        var vendorNs = new HashSet<string>();
        void CollectExt(Dictionary<string,string>? ext)
        {
            if (ext == null) return;
            foreach (var k in ext.Keys)
            {
                var elemNs = ParseElementNamespace(k);
                if (elemNs != null && elemNs != Bpmn.NamespaceName)
                    vendorNs.Add(elemNs);
            }
        }
        foreach (var e in model.Events) CollectExt(e.ExtensionAttributes);
        foreach (var g in model.Gateways) CollectExt(g.ExtensionAttributes);
        foreach (var s in model.Subprocesses) CollectExt(s.ExtensionAttributes);
        foreach (var t in model.Tasks) CollectExt(t.Attributes);
        foreach (var f in model.SequenceFlows)
        {
            CollectExt(f.ExtensionAttributes);
            if (f.Priority.HasValue) vendorNs.Add("http://vertexbpmn.io/schema/1.0");
        }
        // Loop vendor usage
        foreach (var sp in model.Subprocesses)
        {
            if (sp.Loop is MultiInstanceLoopCharacteristics mi)
            {
                if (!string.IsNullOrWhiteSpace(mi.Collection) || !string.IsNullOrWhiteSpace(mi.ElementVariable)) vendorNs.Add("http://camunda.org/schema/1.0/bpmn");
                if (!string.IsNullOrWhiteSpace(mi.InputElement) || !string.IsNullOrWhiteSpace(mi.OutputElement)) vendorNs.Add("http://zeebe.io/schema/zeebe/1.0");
            }
        }

        var definitions = new XElement(Bpmn + "definitions");
        // Declare namespaces (default bpmn prefix for clarity, though spec often uses default)
        definitions.Add(new XAttribute(XNamespace.Xmlns + "bpmn", Bpmn.NamespaceName));
        foreach (var uri in vendorNs)
        {
            if (!WellKnownPrefixes.TryGetValue(uri, out var prefix))
            {
                prefix = uri.Contains('/') ? uri.TrimEnd('/').Split('/').Last().Replace('.', '_').Replace('-', '_') : "ns";
            }
            definitions.SetAttributeValue(XNamespace.Xmlns + prefix, uri);
        }

        // Optional collaboration
        if ((model.Participants?.Count > 0) || (model.MessageFlows?.Count > 0))
        {
            var collab = new XElement(Bpmn + "collaboration");
            foreach (var p in model.Participants ?? Array.Empty<BpmnParticipant>())
            {
                var pEl = new XElement(Bpmn + "participant", new XAttribute("id", p.Id));
                if (!string.IsNullOrWhiteSpace(p.Name)) pEl.Add(new XAttribute("name", p.Name));
                if (!string.IsNullOrWhiteSpace(p.ProcessRef)) pEl.Add(new XAttribute("processRef", p.ProcessRef));
                collab.Add(pEl);
            }
            foreach (var mf in model.MessageFlows ?? Array.Empty<BpmnMessageFlow>())
            {
                var mfEl = new XElement(Bpmn + "messageFlow", new XAttribute("id", mf.Id), new XAttribute("sourceRef", mf.SourceRef), new XAttribute("targetRef", mf.TargetRef));
                if (!string.IsNullOrWhiteSpace(mf.Name)) mfEl.Add(new XAttribute("name", mf.Name));
                collab.Add(mfEl);
            }
            definitions.Add(collab);
        }

        var proc = new XElement(Bpmn + "process", new XAttribute("id", model.ProcessId));
        definitions.Add(proc);

        void AddExtensions(XElement parent, Dictionary<string,string>? ext)
        {
            if (ext == null || ext.Count == 0) return;
            var extRoot = new XElement(Bpmn + "extensionElements");
            var elementMap = new Dictionary<string,XElement>();
            foreach (var kv in ext)
            {
                if (!TryParseExtensionKey(kv.Key, out var nsUri, out var localName, out var attrName)) continue;
                var key = nsUri + "|" + localName;
                if (!elementMap.TryGetValue(key, out var el))
                {
                    XNamespace nsx = nsUri;
                    el = new XElement(nsx + localName);
                    elementMap[key] = el;
                    extRoot.Add(el);
                }
                if (attrName == "__present") continue;
                el.SetAttributeValue(attrName, kv.Value);
            }
            if (elementMap.Count > 0)
                parent.Add(extRoot);
        }

        foreach (var sp in model.Subprocesses)
        {
            var spEl = new XElement(Bpmn + "subProcess", new XAttribute("id", sp.Id));
            if (sp.IsEventSubprocess) spEl.Add(new XAttribute("triggeredByEvent", "true"));
            if (sp.IsTransaction) spEl.Add(new XAttribute("transaction", "true"));
            if (sp.Loop is MultiInstanceLoopCharacteristics mi)
            {
                var miEl = new XElement(Bpmn + "multiInstanceLoopCharacteristics");
                if (mi.IsSequential) miEl.Add(new XAttribute("isSequential", "true"));
                if (mi.LoopCardinality.HasValue) miEl.Add(new XElement(Bpmn + "loopCardinality", mi.LoopCardinality.Value));
                if (!string.IsNullOrWhiteSpace(mi.Collection))
                {
                    // Serialize as camunda:collection attribute for fidelity; cannot infer original source (zeebe inputCollection)
                    miEl.Add(new XAttribute(XName.Get("collection", "http://camunda.org/schema/1.0/bpmn"), mi.Collection));
                }
                if (!string.IsNullOrWhiteSpace(mi.ElementVariable))
                {
                    miEl.Add(new XAttribute(XName.Get("elementVariable", "http://camunda.org/schema/1.0/bpmn"), mi.ElementVariable));
                }
                // Preserve input/output element nodes (Zeebe extensions)
                if (!string.IsNullOrWhiteSpace(mi.InputElement))
                {
                    miEl.Add(new XElement(XName.Get("inputElement", "http://zeebe.io/schema/zeebe/1.0"), mi.InputElement));
                }
                if (!string.IsNullOrWhiteSpace(mi.OutputElement))
                {
                    miEl.Add(new XElement(XName.Get("outputElement", "http://zeebe.io/schema/zeebe/1.0"), mi.OutputElement));
                }
                if (!string.IsNullOrWhiteSpace(mi.CompletionCondition)) miEl.Add(new XElement(Bpmn + "completionCondition", mi.CompletionCondition));
                spEl.Add(miEl);
            }
            else if (sp.Loop is StandardLoopCharacteristics std)
            {
                var stdEl = new XElement(Bpmn + "standardLoopCharacteristics");
                if (std.TestBefore) stdEl.Add(new XAttribute("testBefore", "true"));
                if (std.LoopMaximum.HasValue) stdEl.Add(new XAttribute("loopMaximum", std.LoopMaximum.Value));
                if (!string.IsNullOrWhiteSpace(std.LoopCondition)) stdEl.Add(new XElement(Bpmn + "loopCondition", std.LoopCondition));
                spEl.Add(stdEl);
            }
            AddExtensions(spEl, sp.ExtensionAttributes);
            proc.Add(spEl);
        }

        foreach (var evt in model.Events)
        {
            var evtEl = new XElement(Bpmn + evt.Type, new XAttribute("id", evt.Id));
            foreach (var def in evt.Definitions)
                evtEl.Add(SerializeEventDefinition(def));
            AddExtensions(evtEl, evt.ExtensionAttributes);
            proc.Add(evtEl);
        }

        foreach (var gw in model.Gateways)
        {
            var gwEl = new XElement(Bpmn + gw.Type, new XAttribute("id", gw.Id));
            if (!string.IsNullOrWhiteSpace(gw.DefaultFlowId)) gwEl.Add(new XAttribute("default", gw.DefaultFlowId));
            AddExtensions(gwEl, gw.ExtensionAttributes);
            proc.Add(gwEl);
        }

        foreach (var task in model.Tasks)
        {
            var taskEl = new XElement(Bpmn + task.Type, new XAttribute("id", task.Id));
            AddExtensions(taskEl, task.Attributes);
            proc.Add(taskEl);
        }

        foreach (var f in model.SequenceFlows)
        {
            var fEl = new XElement(Bpmn + "sequenceFlow", new XAttribute("id", f.Id), new XAttribute("sourceRef", f.SourceRef), new XAttribute("targetRef", f.TargetRef));
            if (f.Priority.HasValue)
            {
                fEl.SetAttributeValue(XName.Get("priority", "http://vertexbpmn.io/schema/1.0"), f.Priority.Value);
            }
            if (!string.IsNullOrWhiteSpace(f.ConditionExpression)) fEl.Add(new XElement(Bpmn + "conditionExpression", new XCData(f.ConditionExpression)));
            AddExtensions(fEl, f.ExtensionAttributes);
            proc.Add(fEl);
        }

        foreach (var d in model.DataObjects)
            proc.Add(new XElement(Bpmn + "dataObject", new XAttribute("id", d.Id), d.Name != null ? new XAttribute("name", d.Name) : null));
        foreach (var dr in model.DataObjectReferences)
            proc.Add(new XElement(Bpmn + "dataObjectReference", new XAttribute("id", dr.Id), new XAttribute("dataObjectRef", dr.DataObjectRef)));
        foreach (var ds in model.DataStores)
            proc.Add(new XElement(Bpmn + "dataStore", new XAttribute("id", ds.Id), ds.Name != null ? new XAttribute("name", ds.Name) : null));
        foreach (var dsr in model.DataStoreReferences)
            proc.Add(new XElement(Bpmn + "dataStoreReference", new XAttribute("id", dsr.Id), new XAttribute("dataStoreRef", dsr.DataStoreRef)));
        foreach (var p in model.Properties)
            proc.Add(new XElement(Bpmn + "property", new XAttribute("id", p.Id), p.Name != null ? new XAttribute("name", p.Name) : null));

        foreach (var io in model.ActivityIo)
        {
            var act = proc.Elements().FirstOrDefault(e => (string?)e.Attribute("id") == io.ActivityId);
            if (act == null) continue;
            var ioSpec = new XElement(Bpmn + "ioSpecification");
            foreach (var i in io.DataInputs) ioSpec.Add(new XElement(Bpmn + "dataInput", new XAttribute("id", i.Id), i.Name != null ? new XAttribute("name", i.Name) : null));
            foreach (var o in io.DataOutputs) ioSpec.Add(new XElement(Bpmn + "dataOutput", new XAttribute("id", o.Id), o.Name != null ? new XAttribute("name", o.Name) : null));
            act.Add(ioSpec);
            foreach (var ina in io.InputAssociations)
                act.Add(new XElement(Bpmn + "dataInputAssociation", new XElement(Bpmn + "sourceRef", ina.SourceRef), new XElement(Bpmn + "targetRef", ina.TargetRef)));
            foreach (var oa in io.OutputAssociations)
                act.Add(new XElement(Bpmn + "dataOutputAssociation", new XElement(Bpmn + "sourceRef", oa.SourceRef), new XElement(Bpmn + "targetRef", oa.TargetRef)));
        }

        // Serialize DI if shapes/edges present
        if ((model.Shapes?.Count > 0) || (model.Edges?.Count > 0))
        {
            var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
            var omgdc = (XNamespace)"http://www.omg.org/spec/DD/20100524/DC";
            var omgdi = (XNamespace)"http://www.omg.org/spec/DD/20100524/DI";
            // ensure namespace declarations
            if (definitions.Attribute(XNamespace.Xmlns + "bpmndi") == null)
                definitions.SetAttributeValue(XNamespace.Xmlns + "bpmndi", bpmndi.NamespaceName);
            if (definitions.Attribute(XNamespace.Xmlns + "omgdc") == null)
                definitions.SetAttributeValue(XNamespace.Xmlns + "omgdc", omgdc.NamespaceName);
            if (definitions.Attribute(XNamespace.Xmlns + "omgdi") == null)
                definitions.SetAttributeValue(XNamespace.Xmlns + "omgdi", omgdi.NamespaceName);
            var diagram = new XElement(bpmndi + "BPMNDiagram",
                new XAttribute("id", $"{model.ProcessId}_diagram"));
            var plane = new XElement(bpmndi + "BPMNPlane", new XAttribute("bpmnElement", model.ProcessId));
            diagram.Add(plane);
            if (model.Shapes != null)
            {
                foreach (var s in model.Shapes)
                {
                    plane.Add(new XElement(bpmndi + "BPMNShape",
                        new XAttribute("id", s.Id),
                        new XAttribute("bpmnElement", s.BpmnElementId),
                        new XElement(omgdc + "Bounds",
                            new XAttribute("x", s.X),
                            new XAttribute("y", s.Y),
                            new XAttribute("width", s.Width),
                            new XAttribute("height", s.Height))));
                }
            }
            if (model.Edges != null)
            {
                foreach (var e in model.Edges)
                {
                    plane.Add(new XElement(bpmndi + "BPMNEdge",
                        new XAttribute("id", e.Id),
                        new XAttribute("bpmnElement", e.BpmnElementId),
                        e.Waypoints.Select(wp => new XElement(omgdi + "waypoint", new XAttribute("x", wp.X), new XAttribute("y", wp.Y)))));
                }
            }
            definitions.Add(diagram);
        }

        var doc = new XDocument(definitions);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static bool TryParseExtensionKey(string key, out string nsUri, out string localName, out string attrName)
    {
        nsUri = localName = attrName = string.Empty;
        var parts = key.Split(':');
        if (parts.Length < 2) return false;
        attrName = parts[^1];
        var elemPart = string.Join(':', parts[..^1]); // recombine in case ':' appears in expanded name
        if (elemPart.StartsWith("{"))
        {
            var close = elemPart.IndexOf('}');
            if (close <= 1) return false;
            nsUri = elemPart[1..close];
            localName = elemPart[(close + 1)..];
        }
        else
        {
            localName = elemPart; // no namespace
        }
        return !string.IsNullOrEmpty(localName);
    }

    private static string? ParseElementNamespace(string key)
    {
        var brace = key.IndexOf('}');
        if (key.StartsWith('{') && brace > 1)
            return key.Substring(1, brace - 1);
        return null;
    }

    private static XElement SerializeEventDefinition(EventDefinition def) => def switch
    {
        TimerEventDefinition t => new XElement(Bpmn + "timerEventDefinition",
            t.TimeDate != null ? new XElement(Bpmn + "timeDate", t.TimeDate) : null,
            t.TimeDuration != null ? new XElement(Bpmn + "timeDuration", t.TimeDuration) : null,
            t.TimeCycle != null ? new XElement(Bpmn + "timeCycle", t.TimeCycle) : null),
        MessageEventDefinition m => new XElement(Bpmn + "messageEventDefinition", new XAttribute("messageRef", m.MessageRef)),
        SignalEventDefinition s => new XElement(Bpmn + "signalEventDefinition", new XAttribute("signalRef", s.SignalRef)),
        ErrorEventDefinition e => new XElement(Bpmn + "errorEventDefinition", new XAttribute("errorRef", e.ErrorRef)),
        ConditionalEventDefinition c => new XElement(Bpmn + "conditionalEventDefinition", new XElement(Bpmn + "conditionExpression", new XCData(c.Condition ?? string.Empty))),
        TerminateEventDefinition => new XElement(Bpmn + "terminateEventDefinition"),
        CancelEventDefinition => new XElement(Bpmn + "cancelEventDefinition"),
        CompensationEventDefinition cmp => new XElement(Bpmn + "compensateEventDefinition", cmp.ActivityRef != null ? new XAttribute("activityRef", cmp.ActivityRef) : null),
        EscalationEventDefinition esc => new XElement(Bpmn + "escalationEventDefinition", new XAttribute("escalationRef", esc.EscalationRef)),
        LinkEventDefinition link => new XElement(Bpmn + "linkEventDefinition", new XAttribute("name", link.Name)),
        _ => new XElement(Bpmn + "unsupportedEventDefinition")
    };
}
