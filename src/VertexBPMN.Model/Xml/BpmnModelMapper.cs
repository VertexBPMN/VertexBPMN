
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Extensions;
using Task = VertexBPMN.Domain.Model.Bpmn.Task;

namespace VertexBPMN.Domain.Model
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
                //Name = model.Name,
                TargetNamespace = targetNamespace ?? Ns.BPMNIO.NamespaceName
            };

            var process = new Process
            {
                Id = string.IsNullOrWhiteSpace(model.ProcessId) ? $"proc_{Guid.NewGuid():N}" : model.ProcessId,
                Name = model.Name
            };

            // Process properties
            if (model.Properties != null && model.Properties.Count > 0)
                process.Properties.AddRange(model.Properties);

            // FlowElements
            AddRange(process.FlowElements, model.Events?.Cast<FlowElement>());
            AddRange(process.FlowElements, model.Gateways?.Cast<FlowElement>());
            AddRange(process.FlowElements, model.Subprocesses?.Cast<FlowElement>());
            AddRange(process.FlowElements, model.Tasks?.Cast<FlowElement>());
            AddRange(process.FlowElements, model.SequenceFlows?.Cast<FlowElement>());
            AddRange(process.FlowElements, model.DataObjects?.Cast<FlowElement>());
            AddRange(process.FlowElements, model.DataObjectReferences?.Cast<FlowElement>());
            AddRange(process.FlowElements, model.DataStoreReferences?.Cast<FlowElement>());

            // Artifacts
            AddRange(process.Artifacts, model.TextAnnotations?.Cast<Artifact>());
            AddRange(process.Artifacts, model.Groups?.Cast<Artifact>());
            AddRange(process.Artifacts, model.Associations?.Cast<Artifact>());

            // Lanes
            if (model.Lanes != null && model.Lanes.Count > 0)
            {
                var laneSet = new LaneSet { Id = $"{process.Id}_lanes", Name = "DefaultLaneSet" };
                laneSet.Lanes.AddRange(model.Lanes);
                process.LaneSets.Add(laneSet);
            }

            // Ensure Activities are present (if provided separately)
            if (model.Activities != null && model.Activities.Count > 0)
            {
                foreach (var a in model.Activities)
                {
                    if (!process.FlowElements.Contains(a))
                        process.FlowElements.Add(a);
                }
            }

            // RootElements
            defs.RootElements.Add(process);
            AddRange(defs.RootElements, model.Messages?.Cast<RootElement>());
            AddRange(defs.RootElements, model.Signals?.Cast<RootElement>());
            AddRange(defs.RootElements, model.Errors?.Cast<RootElement>());
            AddRange(defs.RootElements, model.Escalations?.Cast<RootElement>());
            AddRange(defs.RootElements, model.DataStores?.Cast<RootElement>());

            // Collaboration (participants & message flows)
            if ((model.Participants?.Count ?? 0) > 0 || (model.MessageFlows?.Count ?? 0) > 0)
            {
                var collab = new Collaboration
                {
                    Id = $"{process.Id}_collab",
                    Name = string.IsNullOrWhiteSpace(model.Name) ? "Collaboration" : $"{model.Name} Collaboration"
                };

                AddRange(collab.Participants, model.Participants);
                AddRange(collab.MessageFlows, model.MessageFlows);

                // Default ProcessRef if missing
                for (int i = 0; i < collab.Participants.Count; i++)
                {
                    var participant = collab.Participants[i];
                    if (participant.ProcessRef == null || string.IsNullOrWhiteSpace(participant.ProcessRef.Name))
                    {
                        collab.Participants[i] = participant with { ProcessRef = new XmlQualifiedName(process.Name) };
                    }
                }

                defs.RootElements.Add(collab);
            }

            // DI (BPMNDiagram)
            if ((model.Shapes?.Count ?? 0) > 0 || (model.Edges?.Count ?? 0) > 0 || (model.LabelStyles?.Count ?? 0) > 0)
            {
                var name = string.IsNullOrWhiteSpace(model.Name) ? "Diagram" : $"{model.Name} Diagram";
                var plane = new BpmnPlane()
                {
                    Id = $"{process.Id}_plane",
                    BpmnElement =  new XmlQualifiedName(process.Name)
                };
                var bpmnLabelStyles = new List<BpmnLabelStyle>();
                var diag = new BpmnDiagram
                {
                    Id = $"{process.Id}_diagram",
                    Name = name,
                    BpmnPlane = plane,
                    BpmnLabelStyles = bpmnLabelStyles
                };

                AddRange(diag.BpmnPlane.DiagramElements, model.Shapes);
                AddRange(diag.BpmnPlane.DiagramElements, model.Edges);
                // Label styles
                if (model.LabelStyles != null && model.LabelStyles.Count > 0)
                {
                    foreach (var style in model.LabelStyles)
                    {
                        if (string.IsNullOrWhiteSpace(style.Id))
                            style.Id = $"lblStyle_{Guid.NewGuid():N}";
                        diag.BpmnLabelStyles.Add(style);
                    }
                }
                defs.BpmnDiagrams.Add(diag);
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
                    process.ExtensionElements.AnyElements.Add(el);
                }
            }
            return defs;
        }

        public static BpmnModel FromDefinitions(Definitions defs,
    IDictionary<string, Type>? variableTypeMap = null)
        {
            if (defs == null) throw new ArgumentNullException(nameof(defs));

            var process = defs.RootElements?.OfType<Process>().FirstOrDefault();
            if (process == null)
            {
                return new BpmnModel
                {
                    Name = defs.Id,
                    Definitions = new List<Definitions> { defs },
                    ProcessDefinitions = defs,
                    Diagnostics = new List<string> { "No Process found in Definitions.RootElement." },
                    ProcessId = defs.Id
                };
            }

            var events = process.FlowElements.OfType<Event>().Select(e => (Event)UnmapFlowElement(e)).ToList();
            var gateways = process.FlowElements.OfType<Gateway>().Select(g => (Gateway)UnmapFlowElement(g)).ToList();
            var subprocesses = process.FlowElements.OfType<SubProcess>().Select(sp => (SubProcess)UnmapFlowElement(sp)).ToList();
            var tasks = process.FlowElements.OfType<Task>().Select(t => (Task)UnmapFlowElement(t)).ToList();
            var sequenceFlows = process.FlowElements.OfType<SequenceFlow>().Select(sf => (SequenceFlow)UnmapFlowElement(sf)).ToList();
            var dataObjects = process.FlowElements.OfType<DataObject>().Select(dao => (DataObject)UnmapFlowElement(dao)).ToList();
            var dataObjectReferences = process.FlowElements.OfType<DataObjectReference>().Select(dor => (DataObjectReference)UnmapFlowElement(dor)).ToList();
            var dataStoreReferences = process.FlowElements.OfType<DataStoreReference>().Select(dsr => (DataStoreReference)UnmapFlowElement(dsr)).ToList();
            var activities = process.FlowElements.OfType<Activity>().Select(a => (Activity)UnmapFlowElement(a)).ToList();
            var activityIo = process.IoSpecification; // activities?.OfType<Activity>()Where(a => a.IoSpecification != null).ToList();
            var properties = process?.Properties?.ToList() ?? new List<Property>();
            var lanes = (process.LaneSets != null && process.LaneSets.Count > 0)
                ? process.LaneSets.SelectMany(ls => ls.Lanes).ToList()
                : new List<Lane>();
            var textAnnotations = (process.Artifacts != null && process.Artifacts.Count > 0)
                ? process.Artifacts.OfType<TextAnnotation>().Select(ta => (TextAnnotation)UnmapArtifact(ta)).ToList()
                : new List<TextAnnotation>();
            var groups = (process.Artifacts != null && process.Artifacts.Count > 0)
                ? process.Artifacts.OfType<Group>().Select(g => (Group)UnmapArtifact(g)).ToList()
                : new List<Group>();
            var associations = (process.Artifacts != null && process.Artifacts.Count > 0)
                ? process.Artifacts.OfType<Association>().Select(a => (Association)UnmapArtifact(a)).ToList()
                : new List<Association>();
            var messages = defs.RootElements?.OfType<Message>().ToList() ?? new List<Message>();
            var signals = defs.RootElements?.OfType<Signal>().ToList() ?? new List<Signal>();
            var errors = defs.RootElements?.OfType<Error>().ToList() ?? new List<Error>();
            var escalations = defs.RootElements?.OfType<Escalation>().ToList() ?? new List<Escalation>();
            var dataStores = defs.RootElements?.OfType<DataStore>().ToList() ?? new List<DataStore>();
            var collab = defs.RootElements?.OfType<Collaboration>().FirstOrDefault();
            var participants = collab?.Participants?.ToList() ?? new List<Participant>();
            var messageFlows = collab?.MessageFlows?.ToList() ?? new List<MessageFlow>();
            var planeElements = defs.BpmnDiagrams?
                .Where(d => d?.BpmnPlane != null)
                .SelectMany<BpmnDiagram, BpmnPlane>(d => new[] { d.BpmnPlane })
                .ToList() ?? new List<BpmnPlane>();
            var shapes = planeElements.OfType<BpmnShape>().ToList();
            var edges = planeElements.OfType<BpmnEdge>().ToList();
            var styles = defs.BpmnDiagrams?.SelectMany(d => d.BpmnLabelStyles).ToList() ?? new List<BpmnLabelStyle>();
            var labelStyles = styles.GroupBy(s => s.Id ?? string.Empty).Select(g => g.First()).ToList();
            var processVariables = new Dictionary<string, object>();
            if (process.ExtensionElements?.AnyElements?.Count() > 0)
            {
                foreach (var any in process.ExtensionElements.AnyElements)
                {
                    var handler = VariableExtensionRegistry.All.FirstOrDefault(h => h.Matches((XmlElement)any));
                    if (handler == null) continue;
                    var parsed = handler.Deserialize((XmlElement)any, variableTypeMap);
                    foreach (var kv in parsed)
                        processVariables[kv.Key] = kv.Value;
                }
            }

            return new BpmnModel
            {
                Name = process.Name ?? defs.Id,
                Definitions = new List<Definitions> { defs },
                ProcessDefinitions = defs,
                ProcessId = process.Id,
                Events = events,
                Gateways = gateways,
                Subprocesses = subprocesses,
                Tasks = tasks,
                SequenceFlows = sequenceFlows,
                DataObjects = dataObjects,
                DataObjectReferences = dataObjectReferences,
                DataStoreReferences = dataStoreReferences,
                Activities = activities,
                ActivityIo = [activityIo],
                Properties = properties,
                Lanes = lanes,
                TextAnnotations = textAnnotations,
                Groups = groups,
                Associations = associations,
                Messages = messages,
                Signals = signals,
                Errors = errors,
                Escalations = escalations,
                DataStores = dataStores,
                Participants = participants,
                MessageFlows = messageFlows,
                Shapes = shapes,
                Edges = edges,
                LabelStyles = labelStyles,
                ProcessVariables = processVariables
            };
        }
        private static void AddRange<TBase>(List<TBase> target, IEnumerable<TBase>? source)
        {
            if (target == null || source == null) return;
            target.AddRange(source);
        }


        // Mapping helpers
        private static Event UnmapEvent(Event e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            return e switch
            {
                StartEvent se => se,
                EndEvent ee => ee,
                IntermediateCatchEvent ice =>  ice,
                IntermediateThrowEvent ite => ite,
                BoundaryEvent be => be,
                _ => throw new NotSupportedException($"Cannot map Event of type {e.GetType().Name}.")
            };
        }
        private static SubProcess UnmapSubProcess(SubProcess sp) 
        {
            if (sp == null) throw new ArgumentNullException(nameof(sp));

            switch (sp)
            {
              case Transaction t:
                return t;
              case AdHocSubProcess ah:
                return ah;
              default:
                  throw new NotSupportedException($"Cannot map SubProcess of type {sp.GetType().Name}.");
            }
        }
        
        private static Gateway UnmapGateway(Gateway g)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            switch (g)
            {
                case ComplexGateway cg:
                    return cg;
                case EventBasedGateway ebg:
                    return ebg;
                case ExclusiveGateway xg:
                    return xg;
                case InclusiveGateway ig:
                    return ig;
                case ParallelGateway pg:
                    return pg;
                default:
                    throw new NotSupportedException($"Cannot map Gateway of type {g.GetType().Name}.");
            }
        }  
        
        private static Activity UnmapActivity(Activity a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            switch (a)
            {
                case ServiceTask st:
                    return st;
                case UserTask ut:
                    return ut;
                case ManualTask mt:
                    return mt;
                case BusinessRuleTask brt:
                    return brt;
                case ScriptTask sct:
                    return sct;
                case SendTask snt:
                    return snt;
                case ReceiveTask rct:
                    return rct;
                case CallActivity ca:
                    return ca;
                case Transaction tr:
                    return tr;
                case AdHocSubProcess ah:
                    return ah;
                case SubProcess sp:
                    return sp;
                case Task tk:
                    return new Task { Id = tk.Id, Name = tk.Name };
                default:
                    throw new NotSupportedException($"Cannot map Activity of type {a.GetType().Name}.");
            }
        }

        private static Task UnmapTask(Task t) =>
           t switch
            {
                BusinessRuleTask brt => brt,
                ManualTask mt => mt,
                ReceiveTask rct => rct,
                ScriptTask sct => sct,
                SendTask snt => snt,
                ServiceTask st => st,
                UserTask ut => ut,
                _ => null!
            };
        
        // Generic helpers
        private static T MapFlowElement<T>(FlowElement fe) where T : FlowElement => fe as T ?? throw new InvalidCastException();
        private static FlowElement UnmapFlowElement(FlowElement fe) =>
            fe switch
            {
                Task t => UnmapTask(t),
                Event e => UnmapEvent(e),
                Gateway g => UnmapGateway(g),
                SubProcess sp => UnmapSubProcess(sp),
                DataObject d => d,
                DataObjectReference dor => dor,
                DataStoreReference dsr => dsr,
                Activity a => UnmapActivity(a),
                SequenceFlow sf => sf,
                _ => null!
            };

        private static Artifact UnmapArtifact(Artifact a) =>
            a switch
            {
                TextAnnotation ta => ta,
                Group g => g,
                Association asn => asn,
                _ => null!
            };
    }

}