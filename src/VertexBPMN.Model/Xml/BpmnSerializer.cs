using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Model;

public static class BpmnSerializer
{
    private static readonly XmlSerializer Serializer = null;
    private static readonly XmlSerializerNamespaces Namespaces = new();
    static BpmnSerializer()
    {
        try
        {
            Serializer = new XmlSerializer(
                typeof(Definitions),
               null,
                 new Type[]
                        {
                            // RootElement subtypes (from schema)
                            typeof(Collaboration), typeof(Choreography), typeof(GlobalChoreographyTask),
                            typeof(GlobalConversation), typeof(CorrelationProperty), typeof(DataStore), typeof(EndPoint),
                            typeof(Error), typeof(Escalation), typeof(EventDefinition), typeof(CancelEventDefinition),
                            typeof(CompensateEventDefinition), typeof(ConditionalEventDefinition), typeof(ErrorEventDefinition),
                            typeof(EscalationEventDefinition), typeof(LinkEventDefinition), typeof(MessageEventDefinition),
                            typeof(SignalEventDefinition), typeof(TerminateEventDefinition), typeof(TimerEventDefinition),
                            typeof(GlobalBusinessRuleTask), typeof(GlobalManualTask), typeof(GlobalScriptTask), typeof(GlobalTask),
                            typeof(GlobalUserTask), typeof(Interface), typeof(ItemDefinition), typeof(Message), typeof(PartnerEntity),
                            typeof(PartnerRole), typeof(Process), typeof(Resource), typeof(Signal),
                            // FlowElement subtypes (key for Activity chain)
                             typeof(BoundaryEvent), typeof(CallActivity), typeof(Conversation), typeof(EndEvent),
                            typeof(Event), typeof(ExclusiveGateway), typeof(ImplicitThrowEvent), typeof(IntermediateCatchEvent),
                            typeof(IntermediateThrowEvent), typeof(SequenceFlow), typeof(ServiceTask), typeof(StartEvent),
                            typeof(SubProcess), typeof(Bpmn.Task), typeof(UserTask),
                            // Add more as needed (e.g., from document: SubChoreography, TextAnnotation, etc.)
                            typeof(SubChoreography), typeof(TextAnnotation), typeof(Transaction)
                            ,typeof(Activity),typeof(Category),
                        },
                  new XmlRootAttribute("definitions") { Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL" }, null, null);

            Namespaces.Add("bpmn", Ns.BPMN.NamespaceName);
            Namespaces.Add("bpmndi", Ns.BPMNDI.NamespaceName);
            Namespaces.Add("bpmnio", Ns.BPMNIO.NamespaceName);
            Namespaces.Add("dc", Ns.DC.NamespaceName);
            Namespaces.Add("di", Ns.DI.NamespaceName);
            Namespaces.Add("bpmne", Ns.BPMNE.NamespaceName); // Extension namespace
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static readonly Lazy<XmlSchemaSet> _bpmnSchemas = new(() =>
    {
        var set = new XmlSchemaSet();

        set.XmlResolver = null; // Prevent external resolution
        set.Add("http://www.omg.org/spec/BPMN/20100524/MODEL", "Schemas/BPMN20/BPMN20.xsd");
        set.Add("http://www.omg.org/spec/BPMN/20100524/DI", "Schemas/BPMN20/BPMNDI.xsd");
        set.Add("http://www.omg.org/spec/DD/20100524/DC", "Schemas/BPMN20/DC.xsd");
        set.Add("http://www.omg.org/spec/DD/20100524/DI", "Schemas/BPMN20/DI.xsd");
        set.Add("http://www.omg.org/spec/BPMN/20100524/MODEL", "Schemas/BPMN20/Semantic.xsd");
        set.CompilationSettings = new XmlSchemaCompilationSettings { EnableUpaCheck = true };
        set.ValidationEventHandler += (sender, e) => { /* Global handler if needed */ };
        set.Compile();
        return set;
    });
    public static XDocument Write(Definitions defs)
    {
        var bpmn = Ns.BPMN;
        var bpmndi = Ns.BPMNDI;
        var di = Ns.DI;
        var dc = Ns.DC;

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
            foreach (var s in rel.Sources) xr.Add(new XElement("source".B(), s));
            foreach (var t in rel.Targets) xr.Add(new XElement("target".B(), t));
            root.Add(xr);
        }

        // BPMN-DI
        foreach (var d in defs.BpmnDiagrams.OfType<BpmnDiagram>())
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
            new XAttribute("name", x.Name),
            x.ItemRef is null ? null : new XAttribute("itemRef", x.ItemRef)),

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

        Collaboration x when x is not Choreography => WriteCollaboration(x),
        Process x => WriteProcess(x),
        Choreography x => WriteChoreography(x),

        _ => new XElement("rootElement".B(), new XAttribute("id", re.Id ?? Guid.NewGuid().ToString("N")))
    };

    static XElement WriteOperation(Operation op) => new XElement("operation".B(),
        new XAttribute("id", op.Id ?? Guid.NewGuid().ToString("N")),
        new XAttribute("name", op.Name),
        op.ImplementationRef is null ? null : new XAttribute("implementationRef", op.ImplementationRef),
        new XAttribute("inMessageRef", op.InMessageRef.Name ?? ""),
        op.OutMessageRef?.Name is null ? null : new XAttribute("outMessageRef", op.OutMessageRef.Name),
        op.ErrorRefs.Select(er => new XElement("errorRef".B(), er.Name)));

    static XElement WriteProcess(Process p)
    {
        var xe = new XElement("process".B(),
            new XAttribute("id", p.Id ?? Guid.NewGuid().ToString("N")),
            p.Name is null ? null : new XAttribute("name", p.Name),
            p.IsExecutable is false ? null : new XAttribute("isExecutable", p.IsExecutable));

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
                foreach (var fn in l.FlowNodeRefs) xl.Add(new XElement("flowNodeRef".B(), fn));
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
                sf.SourceRef is null ? null : new XAttribute("sourceRef", sf.SourceRef),
                sf.TargetRef is null ? null : new XAttribute("targetRef", sf.TargetRef),
                sf.ConditionExpression is FormalExpression fe2
                    ? new XElement("conditionExpression".B(), fe2.Text.ToString() ?? "")
                    : null),

            // Tasks / Activities

            ServiceTask t => new XElement("serviceTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Name is null ? null : new XAttribute("name", t.Name),
                t.Implementation is null ? null : new XAttribute("implementationRef", t.Implementation)),

            UserTask t => new XElement("userTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.Name is null ? null : new XAttribute("name", t.Name)),

            SendTask t => new XElement("sendTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                t.MessageRef is null ? null : new XAttribute("messageRef", t.MessageRef)),

            ReceiveTask t => new XElement("receiveTask".B(),
                new XAttribute("id", t.Id ?? Guid.NewGuid().ToString("N")),
                new XAttribute("instantiate", t.Instantiate),
                t.MessageRef is null ? null : new XAttribute("messageRef", t.MessageRef)),

            Bpmn.Task t => new XElement("task".B(),
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
                new XAttribute("cancelRemainingInstances", ah.CancelRemainingInstances),
                new XAttribute("ordering", ah.Ordering)),

            SubProcess sp => new XElement("subProcess".B(),
                new XAttribute("id", sp.Id ?? Guid.NewGuid().ToString("N")),
                new XAttribute("triggeredByEvent", sp.TriggeredByEvent),
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
                new XAttribute("instantiate", ebg.Instantiate)),

            // Events
            StartEvent se => new XElement("startEvent".B(),
                new XAttribute("id", se.Id ?? Guid.NewGuid().ToString("N")),
                new XAttribute("isInterrupting", se.IsInterrupting),
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
                new XAttribute("attachedToRef", be.AttachedToRef.Name ?? ""),
                new XAttribute("cancelActivity", be.CancelActivity),
                be.EventDefinitions.Select(WriteEventDefinition)),

           
        };
    }
    static XElement WriteArtifacts(Artifact ar) => ar switch
    {
        // Artifacts
        TextAnnotation ta => new XElement("textAnnotation".B(),
            new XAttribute("id", ta.Id ?? Guid.NewGuid().ToString("N")),
            ta.TextFormat is null ? null : new XAttribute("textFormat", ta.TextFormat),
            ta.Text is null ? null : new XElement("text".B(), ta.Text)),

        Association a => new XElement("association".B(),
            new XAttribute("id", a.Id ?? Guid.NewGuid().ToString("N")),
            a.SourceRef is null ? null : new XAttribute("sourceRef", a.SourceRef),
            a.TargetRef is null ? null : new XAttribute("targetRef", a.TargetRef),
            new XAttribute("associationDirection", a.AssociationDirection.ToString())),

        Group g => new XElement("group".B(),
            new XAttribute("id", g.Id ?? Guid.NewGuid().ToString("N")),
            g.CategoryValueRef is null ? null : new XAttribute("categoryValueRef", g.CategoryValueRef)),


        _ => new XElement("artifact".B(),
            new XAttribute("id", ar.Id ?? Guid.NewGuid().ToString("N")))
    };

    static object WriteEventDefinition(EventDefinition ed) => ed switch
    {
        TimerEventDefinition t => new XElement("timerEventDefinition".B(),
            t.TimeDate is null ? null : new XElement("timeDate".B(), t.TimeDate.Text.ToString() ?? ""),
            t.TimeDuration is null ? null : new XElement("timeDuration".B(), t.TimeDuration.Text.ToString() ?? ""),
            t.TimeCycle is null ? null : new XElement("timeCycle".B(), t.TimeCycle.Text.ToString() ?? "")),

        MessageEventDefinition m => new XElement("messageEventDefinition".B(),
            m.MessageRef is null ? null : new XAttribute("messageRef", m.MessageRef)),

        ErrorEventDefinition e => new XElement("errorEventDefinition".B(),
            e.ErrorRef is null ? null : new XAttribute("errorRef", e.ErrorRef)),

        EscalationEventDefinition es => new XElement("escalationEventDefinition".B(),
            es.EscalationRef is null ? null : new XAttribute("escalationRef", es.EscalationRef)),

        ConditionalEventDefinition c => new XElement("conditionalEventDefinition".B(),
            c.Condition is null ? null : new XElement("condition".B(), c.Condition.Text.ToString() ?? "")),

        LinkEventDefinition l => new XElement("linkEventDefinition".B(),
            l.Name is null ? null : new XAttribute("name", l.Name)),

        SignalEventDefinition s => new XElement("signalEventDefinition".B(),
            s.SignalRef is null ? null : new XAttribute("signalRef", s.SignalRef)),

        CancelEventDefinition => new XElement("cancelEventDefinition".B()),
        CompensateEventDefinition ce => new XElement("compensateEventDefinition".B(),
            ce.ActivityRef is null ? null : new XAttribute("activityRef", ce.ActivityRef)),
        TerminateEventDefinition => new XElement("terminateEventDefinition".B()),
        _ => new XElement("eventDefinition".B())
    };

    static XElement WriteIOSpec(InputOutputSpecification io) => new XElement("ioSpecification".B(),
        io.DataInputs.Select(di => new XElement("dataInput".B(), new XAttribute("id", di.Id ?? Guid.NewGuid().ToString("N")), di.Name is null ? null : new XAttribute("name", di.Name))),
        io.DataOutputs.Select(d => new XElement("dataOutput".B(), new XAttribute("id", d.Id ?? Guid.NewGuid().ToString("N")), d.Name is null ? null : new XAttribute("name", d.Name))),
        io.InputSets.Select(s => new XElement("inputSet".B(), s.DataInputRefs.Select(r => new XElement("dataInputRef".B(), r)))),
        io.OutputSets.Select(s => new XElement("outputSet".B(), s.DataOutputRefs.Select(r => new XElement("dataOutputRef".B(), r)))));

    static XElement WriteCollaboration(Collaboration c)
    {
        var xe = new XElement("collaboration".B(), new XAttribute("id", c.Id ?? Guid.NewGuid().ToString("N")));
        foreach (var p in c.Participants)
            xe.Add(new XElement("participant".B(),
                new XAttribute("id", p.Id ?? Guid.NewGuid().ToString("N")),
                p.Name is null ? null : new XAttribute("name", p.Name),
                p.ProcessRef is null ? null : new XAttribute("processRef", p.ProcessRef)));
        foreach (var mf in c.MessageFlows)
            xe.Add(new XElement("messageFlow".B(),
                new XAttribute("id", mf.Id ?? Guid.NewGuid().ToString("N")),
                mf.Name is null ? null : new XAttribute("name", mf.Name),
                mf.SourceRef is null ? null : new XAttribute("sourceRef", mf.SourceRef!),
                mf.TargetRef is null ? null : new XAttribute("targetRef", mf.TargetRef!),
                mf.MessageRef is null ? null : new XAttribute("messageRef", mf.MessageRef!)));
        foreach (var ar in c.Artifacts)
            xe.Add(new XElement("artifact".B(), WriteArtifacts(ar)));
        return xe;
    }

    static XElement WriteChoreography(Choreography c)
        => new XElement("choreography".B(), new XAttribute("id", c.Id ?? Guid.NewGuid().ToString("N")));

    static XElement WriteDiagram(BpmnDiagram d)
    {
        var x = new XElement("BPMNDiagram".BPMNDI(), new XAttribute("id", d.Id ?? $"diag_{Guid.NewGuid():N}"));
        var plane = new XElement("BPMNPlane".BPMNDI(),
            new XAttribute("id", d.BpmnPlane.Id ?? $"plane_{Guid.NewGuid():N}"),
            d.BpmnPlane.BpmnElement is null ? null : new XAttribute("bpmnElement", d.BpmnPlane.BpmnElement));
        x.Add(plane);

        foreach (var s in d.BpmnPlane.DiagramElements.OfType<BpmnShape>())
        {
            var xs = new XElement("BPMNShape".BPMNDI(),
                new XAttribute("id", s.Id ?? $"shape_{Guid.NewGuid():N}"),
                s.BpmnElement is null ? null : new XAttribute("bpmnElement", s.BpmnElement));
            if (s.Bounds is not null)
                xs.Add(new XElement("Bounds".DC(),
                    new XAttribute("x", s.Bounds.X),
                    new XAttribute("y", s.Bounds.Y),
                    new XAttribute("width", s.Bounds.Width),
                    new XAttribute("height", s.Bounds.Height)));
            plane.Add(xs);
        }

        foreach (var e in d.BpmnPlane.DiagramElements.OfType<BpmnEdge>())
        {
            var xe = new XElement("BPMNEdge".BPMNDI(),
                new XAttribute("id", e.Id ?? $"edge_{Guid.NewGuid():N}"),
                e.BpmnElement is null ? null : new XAttribute("bpmnElement", e.BpmnElement));
            foreach (var p in e.Waypoints)
                xe.Add(new XElement("waypoint".DI(), new XAttribute("x", p.X), new XAttribute("y", p.Y)));
            plane.Add(xe);
        }
        return x;
    }

    //public static string? Serialize(BpmnModel model)
    //{
    //   return Write(model.ProcessDefinitions).ToString();
    //}


    public static string Serialize(BpmnModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var defs = BpmnModelMapper.ToDefinitions(model); // Ensure model is mapped to Definitions if needed

        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, IndentChars = "  " });
        Serializer.Serialize(xw, defs, Namespaces);
        var xml = sw.ToString();
        ValidateXml(xml); // See validation method below
        return xml;
    }
    public static XmlValidationResult ValidateXml(string xml)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = _bpmnSchemas.Value,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            var errors = new List<string>();
            settings.ValidationEventHandler += (sender, e) =>
            {
                errors.Add($"{e.Severity}: {e.Message}");
            };

            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            while (reader.Read()) { } // Read to trigger full validation

            if (errors.Any(e => e.StartsWith("Error:")))
                return new XmlValidationResult(false, errors);
            return new XmlValidationResult(true, Array.Empty<string>());

        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public static BpmnModel Deserialize(string xml)
    {
        try
        {
            using var sr = new StringReader(xml);
            var defs = (Definitions)Serializer.Deserialize(sr);
            var model = BpmnModelMapper.FromDefinitions(defs);
            return model;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}


public record XmlValidationResult(
    bool IsValid,
    IEnumerable<string> Errors
);