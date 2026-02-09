using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace VertexBPMN.Domain.Model.Bpmn
{
    public static class BpmnModelMapper
    {
        private const string BPMN_NS = "http://vertexbpmn.com/bpmn";
        public static Definitions ToDefinitions(BpmnModel model, string? targetNamespace = null, string? exporter = null,
            IDictionary<string, Type>? variableTypeMap = null,
            IEnumerable<string>? vendorNamespaces = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var defs = new Definitions
            {
                Id = string.IsNullOrWhiteSpace(model.ProcessId) ? $"defs_{Guid.NewGuid():N}" : $"{model.ProcessId}_defs",
                Name = model.Name,
                TargetNamespace = targetNamespace ?? Ns.BPMNIO.NamespaceName
            };

            var process = new Process
            {
                Id = string.IsNullOrWhiteSpace(model.ProcessId) ? $"proc_{Guid.NewGuid():N}" : model.ProcessId,
                Name = model.Name
            };

            // Process properties
            if (model.Properties != null && model.Properties.Count > 0)
                process.Property.AddRange(model.Properties);

            // FlowElements
            AddRange(process.FlowElement, model.Events?.Cast<FlowElement>());
            AddRange(process.FlowElement, model.Gateways?.Cast<FlowElement>());
            AddRange(process.FlowElement, model.Subprocesses?.Cast<FlowElement>());
            AddRange(process.FlowElement, model.Tasks?.Cast<FlowElement>());
            AddRange(process.FlowElement, model.SequenceFlows?.Cast<FlowElement>());
            AddRange(process.FlowElement, model.DataObjects?.Cast<FlowElement>());
            AddRange(process.FlowElement, model.DataObjectReferences?.Cast<FlowElement>());
            AddRange(process.FlowElement, model.DataStoreReferences?.Cast<FlowElement>());

            // Artifacts
            AddRange(process.Artifact, model.TextAnnotations?.Cast<Artifact>());
            AddRange(process.Artifact, model.Groups?.Cast<Artifact>());
            AddRange(process.Artifact, model.Associations?.Cast<Artifact>());

            // Lanes
            if (model.Lanes != null && model.Lanes.Count > 0)
            {
                var laneSet = new LaneSet { Id = $"{process.Id}_lanes", Name = "DefaultLaneSet" };
                laneSet.Lane.AddRange(model.Lanes);
                process.LaneSet.Add(laneSet);
            }

            // Ensure Activities are present (if provided separately)
            if (model.Activities != null && model.Activities.Count > 0)
            {
                foreach (var a in model.Activities)
                {
                    if (!process.FlowElement.Contains(a))
                        process.FlowElement.Add(a);
                }
            }

            // RootElements
            defs.RootElement.Add(process);
            AddRange(defs.RootElement, model.Messages?.Cast<RootElement>());
            AddRange(defs.RootElement, model.Signals?.Cast<RootElement>());
            AddRange(defs.RootElement, model.Errors?.Cast<RootElement>());
            AddRange(defs.RootElement, model.Escalations?.Cast<RootElement>());
            AddRange(defs.RootElement, model.DataStores?.Cast<RootElement>());

            // Collaboration (participants & message flows)
            if ((model.Participants?.Count ?? 0) > 0 || (model.MessageFlows?.Count ?? 0) > 0)
            {
                var collab = new Collaboration
                {
                    Id = $"{process.Id}_collab",
                    Name = string.IsNullOrWhiteSpace(model.Name) ? "Collaboration" : $"{model.Name} Collaboration"
                };

                AddRange(collab.Participant, model.Participants);
                AddRange(collab.MessageFlow, model.MessageFlows);

                // Default ProcessRef if missing
                foreach (var p in collab.Participant)
                {
                    if (p.ProcessRef == null || string.IsNullOrWhiteSpace(p.ProcessRef.Name))
                        p.ProcessRef = new XmlQualifiedName(process.Id);
                }

                defs.RootElement.Add(collab);
            }

            // DI (BPMNDiagram)
            if ((model.Shapes?.Count ?? 0) > 0 || (model.Edges?.Count ?? 0) > 0 || (model.LabelStyles?.Count ?? 0) > 0)
            {
                var diag = new BpmnDiagram
                {
                    Id = $"{process.Id}_diagram",
                    Name = string.IsNullOrWhiteSpace(model.Name) ? "Diagram" : $"{model.Name} Diagram",
                    BpmnPlane = new BpmnPlane
                    {
                        Id = $"{process.Id}_plane",
                        BpmnElement = new XmlQualifiedName(process.Id, defs.TargetNamespace)
                    }
                };

                AddRange(diag.BpmnPlane.DiagramElement, model.Shapes?.Cast<DiagramElement>());
                AddRange(diag.BpmnPlane.DiagramElement, model.Edges?.Cast<DiagramElement>());
                // Label styles
                if (model.LabelStyles != null && model.LabelStyles.Count > 0)
                {
                    foreach (var style in model.LabelStyles)
                    {
                        if (string.IsNullOrWhiteSpace(style.Id))
                            style.Id = $"lblStyle_{Guid.NewGuid():N}";
                        diag.BpmnLabelStyle.Add(style);
                    }
                }
                defs.BpmnDiagram.Add(diag);
            }

            if (model.ProcessVariables != null && model.ProcessVariables.Count > 0)
            {
                process.ExtensionElements ??= new ExtensionElements();
                var doc = new XmlDocument();

                var handlers = (vendorNamespaces == null || !vendorNamespaces.Any())
                    ? VariableExtensionRegistry.All
                    : vendorNamespaces.Select(ns => VariableExtensionRegistry.ResolveByNamespace(ns)).Where(h => h != null)!;

                foreach (var handler in handlers)
                {
                    var el = handler!.Serialize(doc, model.ProcessVariables, variableTypeMap);
                    process.ExtensionElements.Any.Add(el);
                }
            }
            return defs;
        }

        public static BpmnModel FromDefinitions(Definitions defs,
            IDictionary<string, Type>? variableTypeMap = null)
        {
            if (defs == null) throw new ArgumentNullException(nameof(defs));

            var model = new BpmnModel
            {
                Name = defs.Name,
                Definitions = new List<Definitions> { defs },
                ProcessDefinitions = defs,
                Diagnostics = new List<string>(),
                // Initialize collections to avoid nulls
                Events = new List<Event>(),
                Gateways = new List<Gateway>(),
                Subprocesses = new List<SubProcess>(),
                SequenceFlows = new List<SequenceFlow>(),
                Tasks = new List<Task>(),
                DataObjects = new List<DataObject>(),
                DataObjectReferences = new List<DataObjectReference>(),
                DataStores = new List<DataStore>(),
                DataStoreReferences = new List<DataStoreReference>(),
                Properties = new List<Property>(),
                ActivityIo = new List<Activity>(),
                Messages = new List<Message>(),
                Signals = new List<Signal>(),
                Errors = new List<Error>(),
                Escalations = new List<Escalation>(),
                Shapes = new List<BpmnShape>(),
                Edges = new List<BpmnEdge>(),
                Participants = new List<Participant>(),
                Lanes = new List<Lane>(),
                MessageFlows = new List<MessageFlow>(),
                TextAnnotations = new List<TextAnnotation>(),
                Associations = new List<Association>(),
                Groups = new List<Group>(),
                ProcessVariables = new Dictionary<string, object>(),
                Activities = new List<Activity>()
            };

            var process = defs.RootElement?.OfType<Process>().FirstOrDefault();
            if (process == null)
            {
                model.Diagnostics.Add("No Process found in Definitions.RootElement.");
                model.ProcessId = defs.Id; // best-effort fallback
                return model;
            }

            model.ProcessId = process.Id;
            model.Name ??= process.Name;

            // FlowElements
            model.Events = process.FlowElement.OfType<Event>().ToList();
            model.Gateways = process.FlowElement.OfType<Gateway>().ToList();
            model.Subprocesses = process.FlowElement.OfType<SubProcess>().ToList();
            model.Tasks = process.FlowElement.OfType<Task>().ToList();
            model.SequenceFlows = process.FlowElement.OfType<SequenceFlow>().ToList();
            model.DataObjects = process.FlowElement.OfType<DataObject>().ToList();
            model.DataObjectReferences = process.FlowElement.OfType<DataObjectReference>().ToList();
            model.DataStoreReferences = process.FlowElement.OfType<DataStoreReference>().ToList();

            // Activities and ActivityIo
            model.Activities = process.FlowElement.OfType<Activity>().ToList();
            model.ActivityIo = model.Activities.Where(a => a.IoSpecification != null).ToList();

            // Properties
            model.Properties = process.Property?.ToList() ?? new List<Property>();

            // Lanes
            if (process.LaneSet != null && process.LaneSet.Count > 0)
                model.Lanes = process.LaneSet.SelectMany(ls => ls.Lane).ToList();

            // Artifacts
            if (process.Artifact != null && process.Artifact.Count > 0)
            {
                model.TextAnnotations = process.Artifact.OfType<TextAnnotation>().ToList();
                model.Groups = process.Artifact.OfType<Group>().ToList();
                model.Associations = process.Artifact.OfType<Association>().ToList();
            }

            // RootElements (non-Process)
            model.Messages = defs.RootElement?.OfType<Message>().ToList() ?? new List<Message>();
            model.Signals = defs.RootElement?.OfType<Signal>().ToList() ?? new List<Signal>();
            model.Errors = defs.RootElement?.OfType<Error>().ToList() ?? new List<Error>();
            model.Escalations = defs.RootElement?.OfType<Escalation>().ToList() ?? new List<Escalation>();
            model.DataStores = defs.RootElement?.OfType<DataStore>().ToList() ?? new List<DataStore>();

            // Collaboration
            var collab = defs.RootElement?.OfType<Collaboration>().FirstOrDefault();
            if (collab != null)
            {
                model.Participants = collab.Participant?.ToList() ?? new List<Participant>();
                model.MessageFlows = collab.MessageFlow?.ToList() ?? new List<MessageFlow>();
            }

            // DI
            var planeElements = defs.BpmnDiagram?
                .Where(d => d?.BpmnPlane != null)
                .SelectMany(d => d.BpmnPlane.DiagramElement)
                .ToList() ?? new List<DiagramElement>();

            model.Shapes = planeElements.OfType<BpmnShape>().ToList();
            model.Edges = planeElements.OfType<BpmnEdge>().ToList();
            // Collect label styles from all diagrams and dedupe by Id
            var styles = defs.BpmnDiagram?.SelectMany(d => d.BpmnLabelStyle).ToList() ?? new List<BpmnLabelStyle>();
            model.LabelStyles = styles.GroupBy(s => s.Id ?? string.Empty).Select(g => g.First()).ToList();
            // Deserialize ProcessVariables from any known vendor block
            if (process.ExtensionElements?.Any?.Count > 0)
            {
                foreach (var any in process.ExtensionElements.Any)
                {
                    var handler = VariableExtensionRegistry.All.FirstOrDefault(h => h.Matches(any));
                    if (handler == null) continue;

                    var parsed = handler.Deserialize(any, variableTypeMap);
                    foreach (var kv in parsed)
                        model.ProcessVariables![kv.Key] = kv.Value; // merge (last wins)
                }
            }
            
            return model;
        }

        private static void AddRange<TBase>(List<TBase> target, IEnumerable<TBase>? source)
        {
            if (target == null || source == null) return;
            target.AddRange(source);
        }


        // Mapping helpers
        private static Task MapTask(Task t) => new() { Id = t.Id, Name = t.Name, Incoming = t.Incoming, Outgoing = t.Outgoing };
        private static Event MapEvent(Event e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            return e switch
            {
                StartEvent se => new StartEvent { Id = se.Id, Name = se.Name },
                EndEvent ee => new EndEvent { Id = ee.Id, Name = ee.Name, EventDefinition = ee.EventDefinition },
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
        private static SubProcess MapSubProcess(SubProcess sp) => new() { Id = sp.Id, Name = sp.Name, FlowElement = sp.FlowElement };
        private static DataObject MapDataObject(DataObject d) => new() { Id = d.Id, Name = d.Name, ItemSubjectRef = d.ItemSubjectRef };
        private static DataObjectReference MapDataObjectReference(DataObjectReference dor) => new() { Id = dor.Id, DataObjectRef = dor.DataObjectRef };
        private static DataStore MapDataStore(DataStore ds) => new() { Id = ds.Id, Name = ds.Name, Capacity = ds.Capacity, IsUnlimited = ds.IsUnlimited };
        private static DataStoreReference MapDataStoreReference(DataStoreReference dsr) => new() { Id = dsr.Id, DataStoreRef = dsr.DataStoreRef };
        private static Property MapProperty(Property p) => new() { Id = p.Id, Name = p.Name, ItemSubjectRef = p.ItemSubjectRef, DataState = p.DataState };
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
        private static BpmnEdge MapBpmnEdge(BpmnEdge e) => new() { Id = e.Id, BpmnElement = e.BpmnElement, Waypoint = e.Waypoint };
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

}