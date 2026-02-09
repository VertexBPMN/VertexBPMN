using System;
using System.Linq;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn.Activities;
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

public static class BpmnWriter
{
    public static XDocument Write(Definitions defs)
    {
        var bpmn = XNamespace.Get(Ns.BPMN);
        var bpmndi = XNamespace.Get(Ns.BPMNDI);
        var di = XNamespace.Get(Ns.DI);
        var dc = XNamespace.Get(Ns.DC);

        var root = new XElement(bpmn + "definitions",
            new XAttribute("id", defs.Id ?? "defs_1"),
            new XAttribute("targetNamespace", defs.TargetNamespace),
            new XAttribute(XNamespace.Xmlns + "bpmn", bpmn),
            new XAttribute(XNamespace.Xmlns + "bpmndi", bpmndi),
            new XAttribute(XNamespace.Xmlns + "di", di),
            new XAttribute(XNamespace.Xmlns + "dc", dc));

        // imports
        foreach (var imp in defs.Imports)
            root.Add(new XElement("import".B(),
                new XAttribute("importType", imp.ImportType),
                new XAttribute("location", imp.Location),
                new XAttribute("namespace", imp.Namespace)));

        // RootElements
        foreach (var re in defs.RootElements)
            root.Add(WriteRootElement(re));

        // Relationships
        foreach (var rel in defs.Relationships)
        {
            var xr = new XElement("relationship".B(),
                new XAttribute("type", rel.Type),
                new XAttribute("direction", rel.Direction.ToString()));
            foreach (var s in rel.Sources) xr.Add(new XElement("source".B(), s.Id));
            foreach (var t in rel.Targets) xr.Add(new XElement("target".B(), t.Id));
            root.Add(xr);
        }

        // BPMN-DI
        foreach (var d in defs.Diagrams.OfType<BpmnDiagram>())
            root.Add(WriteDiagram(d));

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    static XElement WriteRootElement(RootElement re) => re switch
    {
        ItemDefinition x => new XElement("itemDefinition".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            x.StructureRef is null ? null : new XAttribute("structureRef", x.StructureRef),
            new XAttribute("isCollection", x.IsCollection)),

        Message x => new XElement("message".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            x.Name is null ? null : new XAttribute("name", x.Name),
            x.ItemRef?.Id is null ? null : new XAttribute("itemRef", x.ItemRef.Id)),

        Resource x => new XElement("resource".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            new XAttribute("name", x.Name)),

        Category x => new XElement("category".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            x.Name is null ? null : new XAttribute("name", x.Name),
            x.CategoryValues.Select(cv => new XElement("categoryValue".B(),
                new XAttribute("id", cv.Id ?? Guid.NewGuid().ToString("N")),
                cv.Value is null ? null : new XAttribute("value", cv.Value)))),

        Error x => new XElement("error".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            x.Name is null ? null : new XAttribute("name", x.Name),
            x.ErrorCode is null ? null : new XAttribute("errorCode", x.ErrorCode),
            x.StructureRef is null ? null : new XAttribute("structureRef", x.StructureRef)),

        Escalation x => new XElement("escalation".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            x.Name is null ? null : new XAttribute("name", x.Name),
            x.EscalationCode is null ? null : new XAttribute("escalationCode", x.EscalationCode),
            x.StructureRef is null ? null : new XAttribute("structureRef", x.StructureRef)),

        Interface x => new XElement("interface".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            new XAttribute("name", x.Name),
            x.ImplementationRef is null ? null : new XAttribute("implementationRef", x.ImplementationRef),
            x.Operations.Select(WriteOperation)),

        Signal x => new XElement("signal".B(),
            new XAttribute("id", x.Id ?? Guid.NewGuid().ToString("N")),
            x.Name is null ? null : new XAttribute("name", x.Name)),

        Collaboration.Collaboration x => WriteCollaboration(x),
        Process x => WriteProcess(x),
        Choreography.Choreography x => WriteChoreography(x),

        _ => new XElement("rootElement".B(), new XAttribute("id", re.Id ?? Guid.NewGuid().ToString("N")))
    };

    static XElement WriteOperation(Operation op) => new XElement("operation".B(),
        new XAttribute("id", op.Id ?? Guid.NewGuid().ToString("N")),
        new XAttribute("name", op.Name),
        op.ImplementationRef is null ? null : new XAttribute("implementationRef", op.ImplementationRef),
        new XAttribute("inMessageRef", op.InMessageRef.Id ?? ""),
        op.OutMessageRef?.Id is null ? null : new XAttribute("outMessageRef", op.OutMessageRef.Id),
        op.ErrorRefs.Select(er => new XElement("errorRef".B(), er.Id)));

    static XElement WriteProcess(Process p)
    {
        var xe = new XElement("process".B(),
            new XAttribute("id", p.Id ?? Guid.NewGuid().ToString("N")),
            p.Name is null ? null : new XAttribute("name", p.Name),
            p.IsExecutable is null ? null : new XAttribute("isExecutable", p.IsExecutable.Value));

        if (p.IoSpecification is not null) xe.Add(WriteIOSpec(p.IoSpecification));
        foreach (var fe in p.FlowElements) xe.Add(WriteFlowElement(fe));

        foreach (var ls in p.LaneSets)
        {
            var xls = new XElement("laneSet".B(),
                new XAttribute("id", ls.Id ?? Guid.NewGuid().ToString("N")),
                ls.Name is null ? null : new XAttribute("name", ls.Name));
            foreach (var l in ls.Lanes)
            {
                var xl = new XElement("lane".B(),
                    new XAttribute("id", l.Id ?? Guid.NewGuid().ToString("N")),
                    l.Name is null ? null : new XAttribute("name", l.Name));
                foreach (var fn in l.FlowNodeRefs) xl.Add(new XElement("flowNodeRef".B(), fn.Id));
                xls.Add(xl);
            }
            xe.Add(xls);
        }
        return xe;
    }

    static XElement WriteFlowElement(FlowElement fe)
    {
        return fe switch
        {
            SequenceFlow sf => new XElement("sequenceFlow".B(),
                new XAttribute("id", sf.Id ?? Guid.NewGuid().ToString("N")),
                sf.SourceRef?.Id is null ? null : new XAttribute("sourceRef", sf.SourceRef.Id),
                sf.TargetRef?.Id is null ? null : new XAttribute("targetRef", sf.TargetRef.Id),
                sf.ConditionExpression is FormalExpression fe2
                    ? new XElement("conditionExpression".B(), fe2.Body ?? "")
                    : null),

            // Tasks / Activities
            ServiceTask t => new XElement("serviceTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Name is null ? null : new XAttribute("name", t.Name),
                t.ImplementationRef is null ? null : new XAttribute("implementationRef", t.ImplementationRef)),

            UserTask t => new XElement("userTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Name is null ? null : new XAttribute("name", t.Name),
                t.ImplementationRef is null ? null : new XAttribute("implementationRef", t.ImplementationRef)),


            ScriptTask t => new XElement("scriptTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.ScriptFormat is null ? null : new XAttribute("scriptFormat", t.ScriptFormat),
                t.Script is null ? null : new XElement("script".B(), t.Script)),

            ManualTask t => new XElement("manualTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Name is null ? null : new XAttribute("name", t.Name)),

            BusinessRuleTask t => new XElement("businessRuleTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Name is null ? null : new XAttribute("name", t.Name)),

            SendTask t => new XElement("sendTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.MessageRef?.Id is null ? null : new XAttribute("messageRef", t.MessageRef.Id)),

            ReceiveTask t => new XElement("receiveTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Instantiate is null ? null : new XAttribute("instantiate", t.Instantiate.Value),
                t.MessageRef?.Id is null ? null : new XAttribute("messageRef", t.MessageRef.Id)),

            Task t => new XElement("task".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Name is null ? null : new XAttribute("name", t.Name)),
            CallActivity ca => new XElement("callActivity".B(),
                new XAttribute("id", ca.Id ?? Guid.NewGuid().ToString("N")),
                new XAttribute("calledElement", ca.CalledElement)),

            // Sub-Process-Varianten
            Transaction tr => new XElement("transaction".B(),
                new XAttribute("id", tr.Id ?? Guid.NewGuid().ToString("N")),
                tr.FlowElements.Select(WriteFlowElement)),

            AdHocSubProcess ah => new XElement("adHocSubProcess".B(),
                new XAttribute("id", ah.Id ?? Guid.NewGuid().ToString("N")),
                ah.CancelRemainingInstances is null ? null : new XAttribute("cancelRemainingInstances", ah.CancelRemainingInstances.Value),
                ah.Ordering is null ? null : new XAttribute("ordering", ah.Ordering)),

            SubProcess sp => new XElement("subProcess".B(),
                new XAttribute("id", sp.Id ?? Guid.NewGuid().ToString("N")),
                sp.TriggeredByEvent is null ? null : new XAttribute("triggeredByEvent", sp.TriggeredByEvent.Value),
                sp.FlowElements.Select(WriteFlowElement)),
            // Gateways
            ExclusiveGateway eg => new XElement("exclusiveGateway".B(),
                new XAttribute("id", eg.Id ?? Guid.NewGuid().ToString("N")),
                eg.Name is null ? null : new XAttribute("name", eg.Name)),

            InclusiveGateway ig => new XElement("inclusiveGateway".B(),
                new XAttribute("id", ig.Id ?? Guid.NewGuid().ToString("N"))),

            ParallelGateway pg => new XElement("parallelGateway".B(),
                new XAttribute("id", pg.Id ?? Guid.NewGuid().ToString("N"))),

            ComplexGateway cg => new XElement("complexGateway".B(),
                new XAttribute("id", cg.Id ?? Guid.NewGuid().ToString("N"))),

            EventBasedGateway ebg => new XElement("eventBasedGateway".B(),
                new XAttribute("id", ebg.Id ?? Guid.NewGuid().ToString("N")),
                ebg.Instantiate is null ? null : new XAttribute("instantiate", ebg.Instantiate.Value)),

            // Events
            StartEvent se => new XElement("startEvent".B(),
                new XAttribute("id", se.Id ?? Guid.NewGuid().ToString("N")),
                se.IsInterrupting is null ? null : new XAttribute("isInterrupting", se.IsInterrupting.Value),
                se.EventDefinitions.Select(WriteEventDefinition)),

            EndEvent ee => new XElement("endEvent".B(),
                new XAttribute("id", ee.Id ?? Guid.NewGuid().ToString("N")),
                ee.EventDefinitions.Select(WriteEventDefinition)),

            IntermediateCatchEvent ice => new XElement("intermediateCatchEvent".B(),
                new XAttribute("id", ice.Id ?? Guid.NewGuid().ToString("N")),
                ice.EventDefinitions.Select(WriteEventDefinition)),

            IntermediateThrowEvent ite => new XElement("intermediateThrowEvent".B(),
                new XAttribute("id", ite.Id ?? Guid.NewGuid().ToString("N")),
                ite.EventDefinitions.Select(WriteEventDefinition)),

            BoundaryEvent be => new XElement("boundaryEvent".B(),
                new XAttribute("id", be.Id ?? Guid.NewGuid().ToString("N")),
                new XAttribute("attachedToRef", be.AttachedToRef.Id ?? ""),
                be.CancelActivity is null ? null : new XAttribute("cancelActivity", be.CancelActivity.Value),
                be.EventDefinitions.Select(WriteEventDefinition)),

            // Artifacts
            TextAnnotation ta => new XElement("textAnnotation".B(),
                new XAttribute("id", ta.Id ?? Guid.NewGuid().ToString("N")),
                ta.TextFormat is null ? null : new XAttribute("textFormat", ta.TextFormat),
                ta.Text is null ? null : new XElement("text".B(), ta.Text)),

            Association a => new XElement("association".B(),
                new XAttribute("id", a.Id ?? Guid.NewGuid().ToString("N")),
                a.SourceRef?.Id is null ? null : new XAttribute("sourceRef", a.SourceRef.Id),
                a.TargetRef?.Id is null ? null : new XAttribute("targetRef", a.TargetRef.Id),
                new XAttribute("associationDirection", a.AssociationDirection.ToString())),

            _ => new XElement("flowElement".B(),
                new XAttribute("id", fe.Id ?? Guid.NewGuid().ToString("N")))
        };
    }

    static object WriteEventDefinition(EventDefinition ed) => ed switch
    {
        TimerEventDefinition t => new XElement("timerEventDefinition".B(),
            t.TimeDate is null ? null : new XElement("timeDate".B(), t.TimeDate.Body ?? ""),
            t.TimeDuration is null ? null : new XElement("timeDuration".B(), t.TimeDuration.Body ?? ""),
            t.TimeCycle is null ? null : new XElement("timeCycle".B(), t.TimeCycle.Body ?? "")),

        MessageEventDefinition m => new XElement("messageEventDefinition".B(),
            m.MessageRef?.Id is null ? null : new XAttribute("messageRef", m.MessageRef.Id)),

        ErrorEventDefinition e => new XElement("errorEventDefinition".B(),
            e.ErrorRef?.Id is null ? null : new XAttribute("errorRef", e.ErrorRef.Id)),

        EscalationEventDefinition es => new XElement("escalationEventDefinition".B(),
            es.EscalationRef?.Id is null ? null : new XAttribute("escalationRef", es.EscalationRef.Id)),

        ConditionalEventDefinition c => new XElement("conditionalEventDefinition".B(),
            c.Condition is null ? null : new XElement("condition".B(), c.Condition.Body ?? "")),

        LinkEventDefinition l => new XElement("linkEventDefinition".B(),
            l.Name is null ? null : new XAttribute("name", l.Name)),

        SignalEventDefinition s => new XElement("signalEventDefinition".B(),
            s.SignalRef?.Id is null ? null : new XAttribute("signalRef", s.SignalRef.Id)),

        CancelEventDefinition => new XElement("cancelEventDefinition".B()),
        CompensationEventDefinition ce => new XElement("compensateEventDefinition".B(),
            ce.ActivityRef?.Id is null ? null : new XAttribute("activityRef", ce.ActivityRef.Id)),
        TerminateEventDefinition => new XElement("terminateEventDefinition".B()),
        _ => new XElement("eventDefinition".B())
    };

    static XElement WriteIOSpec(IOSpecification io) => new XElement("ioSpecification".B(),
        io.DataInputs.Select(di => new XElement("dataInput".B(), new XAttribute("id", di.Id ?? Guid.NewGuid().ToString("N")), di.Name is null ? null : new XAttribute("name", di.Name))),
        io.DataOutputs.Select(d => new XElement("dataOutput".B(), new XAttribute("id", d.Id ?? Guid.NewGuid().ToString("N")), d.Name is null ? null : new XAttribute("name", d.Name))),
        io.InputSets.Select(s => new XElement("inputSet".B(), s.DataInputRefs.Select(r => new XElement("dataInputRef".B(), r.Id)))),
        io.OutputSets.Select(s => new XElement("outputSet".B(), s.DataOutputRefs.Select(r => new XElement("dataOutputRef".B(), r.Id)))));

    static XElement WriteCollaboration(Collaboration.Collaboration c)
    {
        var xe = new XElement("collaboration".B(), new XAttribute("id", c.Id ?? Guid.NewGuid().ToString("N")));
        foreach (var p in c.Participants)
            xe.Add(new XElement("participant".B(),
                new XAttribute("id", p.Id ?? Guid.NewGuid().ToString("N")),
                p.Name is null ? null : new XAttribute("name", p.Name),
                p.ProcessRef?.Id is null ? null : new XAttribute("processRef", p.ProcessRef.Id)));
        foreach (var mf in c.MessageFlows)
            xe.Add(new XElement("messageFlow".B(),
                new XAttribute("id", mf.Id ?? Guid.NewGuid().ToString("N")),
                mf.Name is null ? null : new XAttribute("name", mf.Name),
                mf.SourceRef?.Id is null ? null : new XAttribute("sourceRef", mf.SourceRef.Id!),
                mf.TargetRef?.Id is null ? null : new XAttribute("targetRef", mf.TargetRef.Id!),
                mf.MessageRef?.Id is null ? null : new XAttribute("messageRef", mf.MessageRef.Id!)));
        return xe;
    }

    static XElement WriteChoreography(Choreography.Choreography c)
        => new XElement("choreography".B(), new XAttribute("id", c.Id ?? Guid.NewGuid().ToString("N")));

    static XElement WriteDiagram(BpmnDiagram d)
    {
        var x = new XElement("BPMNDiagram".BPMNDI(), new XAttribute("id", d.Id ?? $"diag_{Guid.NewGuid():N}"));
        var plane = new XElement("BPMNPlane".BPMNDI(),
            new XAttribute("id", d.Plane.Id ?? $"plane_{Guid.NewGuid():N}"),
            d.Plane.BpmnElement.Id is null ? null : new XAttribute("bpmnElement", d.Plane.BpmnElement.Id));
        x.Add(plane);

        foreach (var s in d.Plane.Shapes)
        {
            var xs = new XElement("BPMNShape".BPMNDI(),
                new XAttribute("id", s.Id ?? $"shape_{Guid.NewGuid():N}"),
                s.BpmnElement.Id is null ? null : new XAttribute("bpmnElement", s.BpmnElement.Id));
            if (s.Bounds is not null)
                xs.Add(new XElement("Bounds".DC(),
                    new XAttribute("x", s.Bounds.X),
                    new XAttribute("y", s.Bounds.Y),
                    new XAttribute("width", s.Bounds.Width),
                    new XAttribute("height", s.Bounds.Height)));
            plane.Add(xs);
        }

        foreach (var e in d.Plane.Edges)
        {
            var xe = new XElement("BPMNEdge".BPMNDI(),
                new XAttribute("id", e.Id ?? $"edge_{Guid.NewGuid():N}"),
                e.BpmnElement.Id is null ? null : new XAttribute("bpmnElement", e.BpmnElement.Id));
            foreach (var p in e.Waypoints)
                xe.Add(new XElement("waypoint".DI(), new XAttribute("x", p.X), new XAttribute("y", p.Y)));
            plane.Add(xe);
        }
        return x;
    }
}