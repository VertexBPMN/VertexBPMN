using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn.Activities;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Common.Artifacts;
using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;
using VertexBPMN.Domain.Model.Bpmn.Common.Faults;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;
using VertexBPMN.Domain.Model.Bpmn.Common.Items;
using VertexBPMN.Domain.Model.Bpmn.Common.Messages;
using VertexBPMN.Domain.Model.Bpmn.Common.Resources;
using VertexBPMN.Domain.Model.Bpmn.Data;
using VertexBPMN.Domain.Model.Bpmn.Di;
using VertexBPMN.Domain.Model.Bpmn.Events;
using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Gateways;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Bpmn.Processes;
using VertexBPMN.Domain.Model.Bpmn.Services;

namespace VertexBPMN.Domain.Model.Bpmn.Xml;


public static class BpmnReader
{
    public static Definitions Read(XDocument doc)
    {
        var bpmn = XNamespace.Get(Ns.BPMN);
        var root = doc.Root ?? throw new InvalidOperationException("Missing definitions");
        var defs = new Definitions(id: root.Attr("id"),
            targetNamespace: root.Attr("targetNamespace") ?? "http://example.com");

        foreach (var imp in root.Elements("import".B()))
            defs.Imports.Add(new Import
            {
                ImportType = imp.Attr("importType") ?? "",
                Location = imp.Attr("location") ?? "",
                Namespace = imp.Attr("namespace") ?? ""
            });

        // id -> element
        var idMap = new Dictionary<string, BaseElement>();

        // Pass 1: RootElements anlegen
        foreach (var el in root.Elements())
        {
            if (el.Name.Namespace != bpmn) continue;
            switch (el.Name.LocalName)
            {
                case "itemDefinition":
                    var idef = new ItemDefinition
                    {
                        Id = el.Attr("id"),
                        StructureRef = el.Attr("structureRef"),
                        IsCollection = el.Attr("isCollection") is string value && bool.TryParse(value, out var bc) && bc
                    };
                    defs.RootElements.Add(idef); if (idef.Id is not null) idMap[idef.Id] = idef;
                    break;

                case "message":
                    var msg = new Message { Id = el.Attr("id"), Name = el.Attr("name") };
                    defs.RootElements.Add(msg); if (msg.Id is not null) idMap[msg.Id] = msg;
                    break;

                case "resource":
                    var res = new Resource { Id = el.Attr("id"), Name = el.Attr("name") ?? "" };
                    defs.RootElements.Add(res); if (res.Id is not null) idMap[res.Id] = res;
                    break;

                case "category":
                    var cat = new Category { Id = el.Attr("id"), Name = el.Attr("name") };
                    foreach (var cv in el.Elements("categoryValue".B()))
                    {
                        var v = new CategoryValue { Id = cv.Attr("id"), Value = cv.Attr("value"), Category = cat };
                        cat.CategoryValues.Add(v); if (v.Id is not null) idMap[v.Id] = v;
                    }
                    defs.RootElements.Add(cat); if (cat.Id is not null) idMap[cat.Id] = cat;
                    break;

                case "error":
                    var err = new Error { Id = el.Attr("id"), Name = el.Attr("name"), ErrorCode = el.Attr("errorCode"), StructureRef = el.Attr("structureRef") };
                    defs.RootElements.Add(err); if (err.Id is not null) idMap[err.Id] = err;
                    break;

                case "escalation":
                    var esc = new Escalation { Id = el.Attr("id"), Name = el.Attr("name"), EscalationCode = el.Attr("escalationCode"), StructureRef = el.Attr("structureRef") };
                    defs.RootElements.Add(esc); if (esc.Id is not null) idMap[esc.Id] = esc;
                    break;

                case "interface":
                    var i = new Interface { Id = el.Attr("id"), Name = el.Attr("name") ?? "", ImplementationRef = el.Attr("implementationRef") };
                    foreach (var op in el.Elements("operation".B()))
                        i.Operations.Add(new Operation
                        {
                            Id = op.Attr("id"),
                            Name = op.Attr("name") ?? "",
                            InMessageRef = null
                        });
                    defs.RootElements.Add(i); if (i.Id is not null) idMap[i.Id] = i;
                    break;

                case "signal":
                    var s = new Signal { Id = el.Attr("id"), Name = el.Attr("name") };
                    defs.RootElements.Add(s); if (s.Id is not null) idMap[s.Id] = s;
                    break;

                case "process":
                    var p = new Process { Id = el.Attr("id"), Name = el.Attr("name"), IsExecutable = el.AttrBool("isExecutable") };
                    defs.RootElements.Add(p); if (p.Id is not null) idMap[p.Id] = p;
                    break;

                case "collaboration":
                    var c = new Collaboration.Collaboration { Id = el.Attr("id") };
                    defs.RootElements.Add(c); if (c.Id is not null) idMap[c.Id] = c;
                    break;

                case "choreography":
                    var ch = new Choreography.Choreography { Id = el.Attr("id") };
                    defs.RootElements.Add(ch); if (ch.Id is not null) idMap[ch.Id] = ch;
                    break;

                case "relationship":
                    var rel = new Relationship { Id = el.Attr("id"), Type = el.Attr("type") ?? "" };
                    defs.Relationships.Add(rel); if (rel.Id is not null) idMap[rel.Id] = rel;
                    break;

                case "BPMNDiagram":
                case "BPMNPlane":
                case "BPMNShape":
                case "BPMNEdge":
                    // DI wird unten in einer separaten Schleife gelesen
                    break;
            }
        }

        // Pass 2: Details & Referenzen
        foreach (var el in root.Elements())
        {
            switch (el.Name.LocalName)
            {
                case "message":
                    var msg = (Message)idMap[el.Attr("id")!];
                    var iref = el.Attr("itemRef");
                    if (iref is not null && idMap.TryGetValue(iref, out var ide) && ide is ItemDefinition idf) 
                        msg.ItemRef = idf;
                    break;

                case "interface":
                    var iface = (Interface)idMap[el.Attr("id")!];
                    foreach (var opEl in el.Elements("operation".B()))
                    {
                        var op = iface.Operations.First(x => x.Id == opEl.Attr("id"));
                        op.ImplementationRef = opEl.Attr("implementationRef");
                        if (opEl.Attr("inMessageRef") is string inRef && idMap.TryGetValue(inRef, out var im) && im is Message inMsg) op.InMessageRef = inMsg;
                        if (opEl.Attr("outMessageRef") is string outRef && idMap.TryGetValue(outRef, out var om) && om is Message outMsg) op.OutMessageRef = outMsg;
                        foreach (var er in opEl.Elements("errorRef".B()))
                            if ((string?)er is string eid && idMap.TryGetValue(eid, out var e) && e is Error err) op.ErrorRefs.Add(err);
                    }
                    break;

                case "process":
                    var p = (Process)idMap[el.Attr("id")!];

                    foreach (var child in el.Elements())
                    {
                        if (child.Name == "ioSpecification".B()) { p.IoSpecification = ReadIOSpec(child); continue; }

                        if (child.Name == "laneSet".B())
                        {
                            var ls = new LaneSet { Id = child.Attr("id"), Name = child.Attr("name") };
                            foreach (var ln in child.Elements("lane".B()))
                            {
                                var lane = new Lane { Id = ln.Attr("id"), Name = ln.Attr("name") };
                                foreach (var fnr in ln.Elements("flowNodeRef".B()))
                                    if ((string?)fnr is string id && idMap.TryGetValue(id, out var fn) && fn is FlowNode fnode) lane.FlowNodeRefs.Add(fnode);
                                ls.Lanes.Add(lane);
                            }
                            p.LaneSets.Add(ls);
                            continue;
                        }

                        var fe = ReadFlowElement(child, idMap);
                        if (fe is not null) p.FlowElements.Add(fe);
                    }
                    break;

                case "collaboration":
                    var c = (Collaboration.Collaboration)idMap[el.Attr("id")!];
                    foreach (var child in el.Elements())
                    {
                        switch (child.Name.LocalName)
                        {
                            case "participant":
                                var part = new Participant { Id = child.Attr("id"), Name = child.Attr("name") };
                                if (child.Attr("processRef") is string pref && idMap.TryGetValue(pref, out var pe) && pe is Process pr) part.ProcessRef = pr;
                                c.Participants.Add(part);
                                break;

                            case "messageFlow":
                                var mf = new MessageFlow { Id = child.Attr("id"), Name = child.Attr("name") };
                                if (child.Attr("sourceRef") is string sref && idMap.TryGetValue(sref, out var se)) mf.SourceRef = se;
                                if (child.Attr("targetRef") is string tref && idMap.TryGetValue(tref, out var te)) mf.TargetRef = te;
                                if (child.Attr("messageRef") is string mref && idMap.TryGetValue(mref, out var me) && me is Message m) mf.MessageRef = m;
                                c.MessageFlows.Add(mf);
                                break;
                        }
                    }
                    break;
            }
        }

        // BPMN-DI lesen
        foreach (var xdiag in root.Elements("BPMNDiagram".BPMNDI()))
        {
            var planeEl = xdiag.Element("BPMNPlane".BPMNDI());
            if (planeEl is null) continue;

            var planeBpmnRef = planeEl.Attr("bpmnElement");
            var planeRef = (planeBpmnRef is not null && idMap.TryGetValue(planeBpmnRef, out var be)) ? be : null;

            var plane = new BpmnPlane { Id = planeEl.Attr("id"), BpmnElement = planeRef };

            foreach (var s in planeEl.Elements("BPMNShape".BPMNDI()))
            {
                var bRef = s.Attr("bpmnElement");
                var elRef = (bRef is not null && idMap.TryGetValue(bRef, out var bel)) ? bel : null;
                var shape = new BpmnShape { Id = s.Attr("id"), BpmnElement = elRef };
                var b = s.Element("Bounds".DC());
                if (b is not null)
                    shape.Bounds = new Bounds(
                        double.Parse(b.Attr("x") ?? "0"),
                        double.Parse(b.Attr("y") ?? "0"),
                        double.Parse(b.Attr("width") ?? "0"),
                        double.Parse(b.Attr("height") ?? "0"));
                plane.Shapes.Add(shape);
            }

            foreach (var e in planeEl.Elements("BPMNEdge".BPMNDI()))
            {
                var bRef = e.Attr("bpmnElement");
                var elRef = (bRef is not null && idMap.TryGetValue(bRef, out var bel)) ? bel : null;
                var edge = new BpmnEdge { Id = e.Attr("id"), BpmnElement = elRef };
                foreach (var wp in e.Elements("waypoint".DI()))
                    edge.Waypoints.Add(new Point(double.Parse(wp.Attr("x") ?? "0"), double.Parse(wp.Attr("y") ?? "0")));
                plane.Edges.Add(edge);
            }

            defs.Diagrams.Add(new BpmnDiagram { Id = xdiag.Attr("id"), Plane = plane });
        }

        return defs;
    }

    static IOSpecification ReadIOSpec(XElement x)
    {
        var io = new IOSpecification { Id = x.Attr("id") };
        var inMap = new Dictionary<string, DataInput>();
        var outMap = new Dictionary<string, DataOutput>();

        foreach (var di in x.Elements("dataInput".B()))
        {
            var d = new DataInput { Id = di.Attr("id"), Name = di.Attr("name") };
            io.DataInputs.Add(d); if (d.Id is not null) inMap[d.Id] = d;
        }
        foreach (var @do in x.Elements("dataOutput".B()))
        {
            var d = new DataOutput { Id = @do.Attr("id"), Name = @do.Attr("name") };
            io.DataOutputs.Add(d); if (d.Id is not null) outMap[d.Id] = d;
        }

        foreach (var set in x.Elements("inputSet".B()))
        {
            var s = new InputSet { Id = set.Attr("id") };
            foreach (var r in set.Elements("dataInputRef".B()))
                if ((string?)r is string id && inMap.TryGetValue(id, out var d)) s.DataInputRefs.Add(d);
            io.InputSets.Add(s);
        }
        foreach (var set in x.Elements("outputSet".B()))
        {
            var s = new OutputSet { Id = set.Attr("id") };
            foreach (var r in set.Elements("dataOutputRef".B()))
                if ((string?)r is string id && outMap.TryGetValue(id, out var d)) s.DataOutputRefs.Add(d);
            io.OutputSets.Add(s);
        }
        return io;
    }

    static FlowElement? ReadFlowElement(XElement x, Dictionary<string, BaseElement> idMap)
    {
        FlowElement? fe = x.Name.LocalName switch
        {
            // Activities / Tasks
            "task" => new Task { Id = x.Attr("id"), Name = x.Attr("name") },
            "serviceTask" => new ServiceTask { Id = x.Attr("id"), Name = x.Attr("name"), ImplementationRef = x.Attr("implementationRef") },
            "userTask" => new UserTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "scriptTask" => new ScriptTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "manualTask" => new ManualTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "businessRuleTask" => new BusinessRuleTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "sendTask" => new SendTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "receiveTask" => new ReceiveTask { Id = x.Attr("id"), Name = x.Attr("name"), Instantiate = x.AttrBool("instantiate") },
            "callActivity" => new CallActivity { Id = x.Attr("id"), CalledElement = x.Attr("calledElement") ?? "" },

            // Subprocess
            "subProcess" => new SubProcess { Id = x.Attr("id"), TriggeredByEvent = x.AttrBool("triggeredByEvent") },
            "transaction" => new Transaction { Id = x.Attr("id") },
            "adHocSubProcess" => new AdHocSubProcess { Id = x.Attr("id") },

            // Gateways
            "exclusiveGateway" => new ExclusiveGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "inclusiveGateway" => new InclusiveGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "parallelGateway" => new ParallelGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "complexGateway" => new ComplexGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "eventBasedGateway" => new EventBasedGateway { Id = x.Attr("id"), Name = x.Attr("name"), Instantiate = x.AttrBool("instantiate") },

            // Events
            "startEvent" => new StartEvent { Id = x.Attr("id"), IsInterrupting = x.AttrBool("isInterrupting") },
            "endEvent" => new EndEvent { Id = x.Attr("id") },
            "intermediateCatchEvent" => new IntermediateCatchEvent { Id = x.Attr("id") },
            "intermediateThrowEvent" => new IntermediateThrowEvent { Id = x.Attr("id") },
            "boundaryEvent" => new BoundaryEvent { Id = x.Attr("id"), CancelActivity = x.AttrBool("cancelActivity"), AttachedToRef = null! },

            // SequenceFlow / Artifacts
            "sequenceFlow" => new SequenceFlow { Id = x.Attr("id") },
            "textAnnotation" => new TextAnnotation { Id = x.Attr("id"), TextFormat = x.Attr("textFormat"), Text = x.Element("text".B())?.Value },
            "association" => new Association { Id = x.Attr("id") },

            _ => null
        };

        if (fe is null) return null;
        if (fe.Id is not null) idMap[fe.Id] = fe;

        // child content / refs
        switch (fe)
        {
            case StartEvent se:
                foreach (var ed in x.Elements()) se.EventDefinitions.Add(ReadEventDefinition(ed, idMap));
                break;
            case EndEvent ee:
                foreach (var ed in x.Elements()) ee.EventDefinitions.Add(ReadEventDefinition(ed, idMap));
                break;
            case IntermediateCatchEvent ice:
                foreach (var ed in x.Elements()) ice.EventDefinitions.Add(ReadEventDefinition(ed, idMap));
                break;
            case IntermediateThrowEvent ite:
                foreach (var ed in x.Elements()) ite.EventDefinitions.Add(ReadEventDefinition(ed, idMap));
                break;
            case BoundaryEvent be:
                var att = x.Attr("attachedToRef");
                if (att is not null && idMap.TryGetValue(att, out var bae) && bae is FlowElement fo && fo is Activity act) be.AttachedToRef = act;
                foreach (var ed in x.Elements()) be.EventDefinitions.Add(ReadEventDefinition(ed, idMap));
                break;
            case SubProcess sp:
                foreach (var child in x.Elements())
                {
                    var cfe = ReadFlowElement(child, idMap);
                    if (cfe is not null) sp.FlowElements.Add(cfe);
                }
                break;
            case SequenceFlow sf:
                if (x.Attr("sourceRef") is string sref && idMap.TryGetValue(sref, out var s) && s is FlowNode sn) sf.SourceRef = sn;
                if (x.Attr("targetRef") is string tref && idMap.TryGetValue(tref, out var t) && t is FlowNode tn) sf.TargetRef = tn;
                var cond = x.Element("conditionExpression".B());
                if (cond is not null) sf.ConditionExpression = new FormalExpression { Body = cond.Value };
                break;
            case Association a:
                if (x.Attr("sourceRef") is string asrc && idMap.TryGetValue(asrc, out var sourceRef)) a.SourceRef = sourceRef;
                if (x.Attr("targetRef") is string atgt && idMap.TryGetValue(atgt, out var targetRef)) a.TargetRef = targetRef;
                break;
        }

        return fe;
    }

    static EventDefinition ReadEventDefinition(XElement x, Dictionary<string, BaseElement> idMap)
        => x.Name.LocalName switch
        {
            "timerEventDefinition" => new TimerEventDefinition
            {
                TimeDate = x.Element("timeDate".B()) is XElement td ? new Expression { Body = td.Value } : null,
                TimeDuration = x.Element("timeDuration".B()) is XElement tdu ? new Expression { Body = tdu.Value } : null,
                TimeCycle = x.Element("timeCycle".B()) is XElement tc ? new Expression { Body = tc.Value } : null,
            },
            "messageEventDefinition" => new MessageEventDefinition { MessageRef = x.Attr("messageRef") is string mr && idMap.TryGetValue(mr, out var me) && me is Message m ? m : null },
            "errorEventDefinition" => new ErrorEventDefinition { ErrorRef = x.Attr("errorRef") is string er && idMap.TryGetValue(er, out var ee) && ee is Error e ? e : null },
            "escalationEventDefinition" => new EscalationEventDefinition { EscalationRef = x.Attr("escalationRef") is string er && idMap.TryGetValue(er, out var es) && es is Escalation esc ? esc : null },
            "conditionalEventDefinition" => new ConditionalEventDefinition { Condition = x.Element("condition".B()) is XElement c ? new Expression { Body = c.Value } : null },
            "linkEventDefinition" => new LinkEventDefinition { Name = x.Attr("name") },
            "signalEventDefinition" => new SignalEventDefinition { SignalRef = x.Attr("signalRef") is string sr && idMap.TryGetValue(sr, out var s) && s is Signal sig ? sig : null },
            "cancelEventDefinition" => new CancelEventDefinition(),
            "compensateEventDefinition" => new CompensationEventDefinition { ActivityRef = x.Attr("activityRef") is string ar && idMap.TryGetValue(ar, out var a) && a is Activity act ? act : null },
            "terminateEventDefinition" => new TerminateEventDefinition(),
            _ => null
        };
}