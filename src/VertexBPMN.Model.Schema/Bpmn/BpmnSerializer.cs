using BenchmarkDotNet.Disassemblers;
using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace VertexBPMN.Domain.Model.Bpmn;

using CommandLine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Schema;
using System.Xml.Serialization;

public static class BpmnSerializer
{
    private static readonly XmlSerializer Serializer = new(typeof(Definitions),
        new XmlRootAttribute("definitions") { Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL" });
    private static readonly XmlSerializerNamespaces Namespaces = new();
    static BpmnSerializer()
    {
        Namespaces.Add("bpmn", Ns.BPMN.NamespaceName);
        Namespaces.Add("bpmndi", Ns.BPMNDI.NamespaceName);
        Namespaces.Add("bpmnio", Ns.BPMNIO.NamespaceName);
        Namespaces.Add("dc", Ns.DC.NamespaceName);
        Namespaces.Add("di", Ns.DI.NamespaceName);
        Namespaces.Add("bpmne", Ns.BPMNE.NamespaceName); // Extension namespace
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

    public static string Serialize2(BpmnModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        // Build Process (FlowElements only contain elements valid inside a process)
        var process = new Process
        {
            Id = model.ProcessId,
            Name = model.Name,
            FlowElement = BuildProcessFlowElements(model),
            Artifact = new List<Artifact>(),
            ExtensionElements = MapProcessVariables(model.ProcessVariables),
            Property = model.Properties?.Select(MapProperty).ToList() ?? new List<Property>(),
        };

        // Artifacts (TextAnnotations, Associations, Groups) belong into Process.Artifact (not RootElements)
        if (model.TextAnnotations?.Any() == true)
        {
            foreach (var ta in model.TextAnnotations)
            {
                process.Artifact.Add(new TextAnnotation
                {
                    Id = ta.Id,
                    // TextAnnotation (generated) uses array of string for body text (per xsd pattern)
                    Text = ta.Text,
                    TextFormat = "text/plain"
                });
            }
        }
        if (model.Associations?.Any() == true)
        {
            foreach (var a in model.Associations)
            {
                process.Artifact.Add(new Association
                {
                    Id = a.Id,
                    SourceRef = a.SourceRef,
                    TargetRef = a.TargetRef
                });
            }
        }
        if (model.Groups?.Any() == true)
        {
            // BPMN Group has no Name attribute in schema (uses CategoryValueRef); keep minimal
            foreach (var g in model.Groups)
            {
                process.Artifact.Add(new Group { Id = g.Id });
            }
        }

        // RootElements collection
        var rootElements = new List<RootElement> { process };

        // Optional Collaboration (Participants / MessageFlows)
        if (model.Participants?.Any() == true || model.MessageFlows?.Any() == true)
        {
            var collaboration = new Collaboration
            {
                Id = $"{model.ProcessId}_collaboration",
                Participant = model.Participants?
                    .Select(p => new Participant
                    {
                        Id = p.Id,
                        Name = p.Name,
                        ProcessRef = new System.Xml.XmlQualifiedName(model.ProcessId)
                    })
                    .ToList(),
                MessageFlow = model.MessageFlows?
                    .Select(mf => new MessageFlow
                    {
                        Id = mf.Id,
                        SourceRef = mf.SourceRef,
                        TargetRef = mf.TargetRef
                    })
                    .ToList()
            };
            rootElements.Add(collaboration);
        }

        // DataStores are RootElements (NOT FlowElements)
        if (model.DataStores?.Any() == true)
            rootElements.AddRange(model.DataStores.Select(MapDataStore));

        // Messages / Signals / Errors / Escalations
        if (model.Messages?.Any() == true)
            rootElements.AddRange(model.Messages.Select(m => new Message { Id = m.Id, Name = m.Name }));
        if (model.Signals?.Any() == true)
            rootElements.AddRange(model.Signals.Select(s => new Signal { Id = s.Id, Name = s.Name }));
        if (model.Errors?.Any() == true)
            rootElements.AddRange(model.Errors.Select(e => new Error { Id = e.Id, Name = e.Name }));
        if (model.Escalations?.Any() == true)
            rootElements.AddRange(model.Escalations.Select(es => new Escalation { Id = es.Id, Name = es.Name }));

        // BPMN Diagram (DI)
        List<BpmnDiagram>? diagrams = null;
        if (model.Shapes?.Any() == true || model.Edges?.Any() == true)
        {
            var planeElements = new List<DiagramElement>();
            if (model.Shapes != null) planeElements.AddRange(model.Shapes.Select(MapBpmnShape));
            if (model.Edges != null) planeElements.AddRange(model.Edges.Select(MapBpmnEdge));

            diagrams = new List<BpmnDiagram>
            {
                new()
                {
                    Id = $"{model.ProcessId}_diagram",
                    Name = model.Name,
                    BpmnPlane = new BpmnPlane
                    {
                        Id = $"{model.ProcessId}_plane",
                        BpmnElement = new System.Xml.XmlQualifiedName(process.Id),
                        DiagramElement = planeElements
                    }
                }
            };
        }


        var defs = new Definitions
        {
            TargetNamespace = Ns.BPMNIO.NamespaceName,
            ExpressionLanguage = "http://www.w3.org/1999/XPath",
            TypeLanguage = "http://www.w3.org/2001/XMLSchema",
            RootElement = rootElements,
            BpmnDiagram = diagrams
        };

        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, IndentChars = "  " });
        Serializer.Serialize(xw, defs, Namespaces);
        return sw.ToString();
    }

    public static string SerializeWithValidation<T>(T obj)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true });
        serializer.Serialize(xw, obj);

        var xml = sw.ToString();
        ValidateXml(xml); // See validation method below
        return xml;
    }

    public static XmlValidationResult ValidateXml(string xml)
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
    
    private static List<FlowElement> BuildProcessFlowElements(BpmnModel model)
    {
        var list = new List<FlowElement>();

        if (model.Tasks != null) list.AddRange(model.Tasks.Select(MapTask));
        if (model.Events != null) list.AddRange(model.Events.Select(MapEvent));
        if (model.Gateways != null) list.AddRange(model.Gateways.Select(MapGateway));
        if (model.Subprocesses != null) list.AddRange(model.Subprocesses.Select(MapSubProcess));
        if (model.SequenceFlows != null)
            list.AddRange(model.SequenceFlows.Select(sf => new SequenceFlow
            {
                Id = sf.Id,
                SourceRef = sf.SourceRef,
                TargetRef = sf.TargetRef
            }));
        if (model.DataObjects != null) list.AddRange(model.DataObjects.Select(MapDataObject));
        if (model.DataObjectReferences != null) list.AddRange(model.DataObjectReferences.Select(MapDataObjectReference));
        // DataStores are NOT FlowElements (skip)
        if (model.DataStoreReferences != null) list.AddRange(model.DataStoreReferences.Select(MapDataStoreReference));
        if (model.ActivityIo != null) list.AddRange(model.ActivityIo.Select(MapActivity));
        if (model.Activities != null) list.AddRange(model.Activities.Select(MapActivity));

        return list;
    }

    public static BpmnModel Deserialize(string xml)
    {
        using var sr = new StringReader(xml);
        var defs = (Definitions)Serializer.Deserialize(sr);
        var model = BpmnModelMapper.FromDefinitions(defs);
        return model;
        //var proc = defs.RootElement.OfType<Process>().FirstOrDefault() ?? throw new InvalidOperationException("No process element found in BPMN XML.");
        //var coll = defs.RootElement.OfType<Collaboration>().FirstOrDefault();
        //var diag = defs.BpmnDiagram?.FirstOrDefault()?.BpmnPlane;

        //return new BpmnModel
        //{
        //    ProcessId = proc.Id,
        //    Name = proc.Name,
        //    Tasks = proc.FlowElement.OfType<Task>().Select(UnmapTask).ToList(),
        //    Events = proc.FlowElement.OfType<Event>().Select(UnmapEvent).ToList(),
        //    Gateways = proc.FlowElement.OfType<Gateway>().Select(UnmapGateway).ToList(),
        //    Subprocesses = proc.FlowElement.OfType<SubProcess>().Select(UnmapSubProcess).ToList(),
        //    SequenceFlows = proc.FlowElement.OfType<SequenceFlow>().Select(sf => new SequenceFlow { Id = sf.Id, SourceRef = sf.SourceRef, TargetRef = sf.TargetRef }).ToList(),
        //    DataObjects = proc.FlowElement.OfType<DataObject>().Select(UnmapDataObject).ToList(),
        //    DataObjectReferences = proc.FlowElement.OfType<DataObjectReference>().Select(UnmapDataObjectReference).ToList(),
        //    DataStores = defs.RootElement.OfType<DataStore>().Select(UnmapDataStore).ToList(),
        //    DataStoreReferences = proc.FlowElement.OfType<DataStoreReference>().Select(UnmapDataStoreReference).ToList(),
        //    Properties = proc.Property.OfType<Property>().Select(UnmapProperty).ToList(),
        //    ActivityIo = proc.FlowElement.OfType<Activity>().Select(UnmapActivityIo).ToList(),
        //    Messages = defs.RootElement.OfType<Message>().Select(m => new Message { Id = m.Id, Name = m.Name }).ToList(),
        //    Signals = defs.RootElement.OfType<Signal>().Select(s => new Signal { Id = s.Id, Name = s.Name }).ToList(),
        //    Errors = defs.RootElement.OfType<Error>().Select(e => new Error { Id = e.Id, Name = e.Name }).ToList(),
        //    Escalations = defs.RootElement.OfType<Escalation>().Select(es => new Escalation { Id = es.Id, Name = es.Name }).ToList(),
        //    Shapes = diag?.DiagramElement?.OfType<BpmnShape>().Select(UnmapBpmnShape).ToList() ?? new List<BpmnShape>(),
        //    Edges = diag?.DiagramElement?.OfType<BpmnEdge>().Select(UnmapBpmnEdge).ToList() ?? new List<BpmnEdge>(),
        //    Participants = coll?.Participant?.Select(p => new Participant { Id = p.Id, Name = p.Name }).ToList() ?? new List<Participant>(),
        //    MessageFlows = coll?.MessageFlow?.Select(mf => new MessageFlow { Id = mf.Id, SourceRef = mf.SourceRef, TargetRef = mf.TargetRef }).ToList() ?? new List<MessageFlow>(),
        //    TextAnnotations = proc.Artifact?.OfType<TextAnnotation>().Select(ta => new TextAnnotation { Id = ta.Id, Text = ta.Text }).ToList() ?? new List<TextAnnotation>(),
        //    Associations = proc.Artifact?.OfType<Association>().Select(a => new Association { Id = a.Id, SourceRef = a.SourceRef, TargetRef = a.TargetRef }).ToList() ?? new List<Association>(),
        //    Groups = proc.Artifact?.OfType<Group>().Select(g => new Group { Id = g.Id }).ToList() ?? new List<Group>(),
        //    Activities = proc.FlowElement.OfType<Activity>().Select(UnmapActivity).ToList(),
        //    ProcessVariables = UnmapProcessVariables(proc.ExtensionElements),
        //    ProcessDefinitions = defs
        //};

    }

    public static T Deserialize<T>(string xml)
    {
        return DeserializeWithValidation<T>(xml);
    }

    public static T DeserializeWithValidation<T>(string xml)
    {
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = _bpmnSchemas.Value,
            DtdProcessing = DtdProcessing.Prohibit, // Security: Disable DTDs
            XmlResolver = null // Disable external entity resolution
        };

        var errors = new List<string>();
        settings.ValidationEventHandler += (sender, e) =>
        {
            var msg = $"{e.Severity}: {e.Message} (Line {e.Exception?.LineNumber}, Pos {e.Exception?.LinePosition})";
            errors.Add(msg);
            Console.WriteLine(msg); // Or log
        };

        using var stringReader = new StringReader(xml);
        using var validatingReader = XmlReader.Create(stringReader, settings);
        var serializer = new XmlSerializer(typeof(T));

        try
        {
            var result = (T)serializer.Deserialize(validatingReader);
            if (errors.Any(e => e.StartsWith("Error:"))) throw new XmlSchemaValidationException(string.Join("; ", errors));
            return result;
        }
        catch (InvalidOperationException ex) when (ex.InnerException is XmlSchemaValidationException)
        {
            throw new XmlSchemaValidationException($"Deserialization failed validation: {ex.InnerException.Message}");
        }
    }


    // Mapping helpers
    private static Task MapTask(Task t) => new() { Id = t.Id, Name = t.Name, Incoming = t.Incoming, Outgoing = t.Outgoing };
    private static Event MapEvent(Event e)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));

        return e switch
        {
            StartEvent se => new StartEvent { Id = se.Id, Name = se.Name },
            EndEvent ee => new EndEvent { Id = ee.Id, Name = ee.Name },
            IntermediateCatchEvent ice => new IntermediateCatchEvent { Id = ice.Id, Name = ice.Name },
            IntermediateThrowEvent ite => new IntermediateThrowEvent { Id = ite.Id, Name = ite.Name },
            BoundaryEvent be => new BoundaryEvent { Id = be.Id, Name = be.Name },
            _ => throw new NotSupportedException($"Cannot map Event of type {e.GetType().Name}.")
        };
    }
    private static Gateway MapGateway(Gateway g)
    {
        if (g == null) throw new ArgumentNullException(nameof(g));
        switch (g)
        {
            case ComplexGateway cg:
                return new ComplexGateway { Id = cg.Id, Name = cg.Name, Incoming = cg.Incoming, Outgoing = cg.Outgoing };
            case EventBasedGateway ebg:
                return new EventBasedGateway { Id = ebg.Id, Name = ebg.Name, Incoming = ebg.Incoming, Outgoing = ebg.Outgoing };
            case ExclusiveGateway xg:
                return new ExclusiveGateway { Id = xg.Id, Name = xg.Name, Incoming = xg.Incoming, Outgoing = xg.Outgoing };
            case InclusiveGateway ig:
                return new InclusiveGateway { Id = ig.Id, Name = ig.Name, Incoming = ig.Incoming, Outgoing = ig.Outgoing };
            case ParallelGateway pg:
                return new ParallelGateway { Id = pg.Id, Name = pg.Name, Incoming = pg.Incoming, Outgoing = pg.Outgoing };
            default:
                throw new NotSupportedException($"Cannot map Gateway of type {g.GetType().Name}.");
        }
    }
    private static SubProcess MapSubProcess(SubProcess sp) => new() { Id = sp.Id, Name = sp.Name, FlowElement = sp.FlowElement};
    private static DataObject MapDataObject(DataObject d) => new() { Id = d.Id, Name = d.Name, ItemSubjectRef = d.ItemSubjectRef };
    private static DataObjectReference MapDataObjectReference(DataObjectReference dor) => new() { Id = dor.Id, DataObjectRef = dor.DataObjectRef };
    private static DataStore MapDataStore(DataStore ds) => new() { Id = ds.Id, Name = ds.Name, Capacity = ds.Capacity, IsUnlimited = ds.IsUnlimited };
    private static DataStoreReference MapDataStoreReference(DataStoreReference dsr) => new() { Id = dsr.Id, DataStoreRef = dsr.DataStoreRef };
    private static Property MapProperty(Property p) => new() { Id = p.Id, Name = p.Name, ItemSubjectRef = p.ItemSubjectRef , DataState = p.DataState };
    private static Activity MapActivityIo(Activity a) => new ServiceTask { Id = a.Id, Name = a.Name };
    private static Activity MapActivity(Activity a)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        switch (a)
        {
            case ServiceTask st:
                return MapTask(st);
            case UserTask ut:
                return MapTask(ut);
            case ManualTask mt:
                return MapTask(mt);
            case BusinessRuleTask brt:
                return MapTask(brt);
            case ScriptTask sct:
                return MapTask(sct);
            case SendTask snt:
                return MapTask(snt);
            case ReceiveTask rct:
                return MapTask(rct);
            case CallActivity ca:
                return new CallActivity { Id = ca.Id, Name = ca.Name, CalledElement = ca.CalledElement };
            case Transaction tr:
                return new Transaction { Id = tr.Id, Name = tr.Name, Method = tr.Method };
            case AdHocSubProcess ah:
                return new AdHocSubProcess { Id = ah.Id, Name = ah.Name, CompletionCondition = ah.CompletionCondition };
            case SubProcess sp:
                return MapSubProcess(sp);
            case Task tk:
                return new Task { Id = tk.Id, Name = tk.Name };
            default:
                throw new NotSupportedException($"Cannot map Activity of type {a.GetType().Name}.");
        }
    }
    private static BpmnShape MapBpmnShape(BpmnShape s) => new() { Id = s.Id, BpmnElement = s.BpmnElement, Bounds = s.Bounds };
    private static BpmnEdge MapBpmnEdge(BpmnEdge e) => new() { Id = e.Id, BpmnElement = e.BpmnElement, Waypoint = e.Waypoint};
    private static ExtensionElements MapProcessVariables(Dictionary<string, object>? vars)
    {
        if (vars == null || vars.Count == 0) return new ExtensionElements();
        var doc = new XmlDocument();
        var exts = new ExtensionElements { Any = new List<XmlElement>() };
        foreach (var kv in vars)
        {
            var el = doc.CreateElement("bpmne", "processVariable", "http://www.omg.org/spec/BPMN/20100524/MODEL");
            el.SetAttribute("name", kv.Key);
            el.SetAttribute("type", kv.Value?.GetType().Name ?? "object");
            el.InnerText = kv.Value?.ToString() ?? "";
            exts.Any.Add(el);
        }
        return exts;
    }

    // Unmapping helpers
    private static Task UnmapTask(Task t) => new() { Id = t.Id, Name = t.Name, Incoming = t.Incoming, Outgoing = t.Outgoing };
    private static Event UnmapEvent(Event e)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));

        return e switch
        {
            StartEvent se => new StartEvent { Id = se.Id, Name = se.Name },
            EndEvent ee => new EndEvent { Id = ee.Id, Name = ee.Name },
            IntermediateCatchEvent ice => new IntermediateCatchEvent { Id = ice.Id, Name = ice.Name },
            IntermediateThrowEvent ite => new IntermediateThrowEvent { Id = ite.Id, Name = ite.Name },
            BoundaryEvent be => new BoundaryEvent { Id = be.Id, Name = be.Name },
            _ => throw new NotSupportedException($"Cannot map Event of type {e.GetType().Name}.")
        };
    }
    private static Gateway UnmapGateway(Gateway g) => new() { Id = g.Id, Name = g.Name, Incoming = g.Incoming, Outgoing = g.Outgoing };
    private static SubProcess UnmapSubProcess(SubProcess sp) => new() { Id = sp.Id, Name = sp.Name, FlowElement = sp.FlowElement?.Select(UnmapFlowElement).ToList() };
    private static DataObject UnmapDataObject(DataObject d) => new() { Id = d.Id, Name = d.Name, ItemSubjectRef = d.ItemSubjectRef };
    private static DataObjectReference UnmapDataObjectReference(DataObjectReference dor) => new() { Id = dor.Id, DataObjectRef = dor.DataObjectRef };
    private static DataStore UnmapDataStore(DataStore ds) => new() { Id = ds.Id, Name = ds.Name, Capacity = ds.Capacity, IsUnlimited = ds.IsUnlimited };
    private static DataStoreReference UnmapDataStoreReference(DataStoreReference dsr) => new() { Id = dsr.Id, DataStoreRef = dsr.DataStoreRef };
    private static Property UnmapProperty(Property p) => new() { Id = p.Id, Name = p.Name, ItemSubjectRef = p.ItemSubjectRef, DataState = p.DataState };
    private static Activity UnmapActivityIo(Activity a) => MapActivity(a);
    private static Activity UnmapActivity(Activity a) => MapActivity(a);
    private static BpmnShape UnmapBpmnShape(BpmnShape s) => new() { Id = s.Id, BpmnElement = s.BpmnElement, Bounds = s.Bounds };
    private static BpmnEdge UnmapBpmnEdge(BpmnEdge e) => new() { Id = e.Id, BpmnElement = e.BpmnElement, Waypoint = e.Waypoint };

    private static Dictionary<string, object> UnmapProcessVariables(ExtensionElements exts)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        if (exts?.Any != null)
        {
            foreach (var el in exts.Any)
            {
                if (el.LocalName == "processVariable")
                {
                    var name = el.GetAttribute("name");
                    if (!string.IsNullOrEmpty(name))
                        dict[name] = el.InnerText;
                }
            }
        }
        return dict;
    }

    private static Lane MapLane(Lane l) => new() { Id = l.Id, Name = l.Name };
    private static Lane UnmapLane(Lane l) => new() { Id = l.Id, Name = l.Name };

    // Generic helpers
    private static T MapFlowElement<T>(FlowElement fe) where T : FlowElement => fe as T ?? throw new InvalidCastException();
    private static FlowElement UnmapFlowElement(FlowElement fe) =>
        fe switch
        {
            Task t => UnmapTask(t),
            Event e => UnmapEvent(e),
            Gateway g => UnmapGateway(g),
            SubProcess sp => UnmapSubProcess(sp),
            DataObject d => UnmapDataObject(d),
            DataObjectReference dor => UnmapDataObjectReference(dor),
            DataStoreReference dsr => UnmapDataStoreReference(dsr),
            Activity a => UnmapActivity(a),
            SequenceFlow sf => new SequenceFlow { Id = sf.Id, SourceRef = sf.SourceRef, TargetRef = sf.TargetRef },
            _ => null!
        };
}

public record XmlValidationResult(
    bool IsValid,
    IEnumerable<string> Errors
);
