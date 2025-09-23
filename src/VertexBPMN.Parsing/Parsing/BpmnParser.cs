using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Parsing;

public class BpmnParser : IBpmnParser
{
    private readonly BpmnParserOptions _options;

    // Simple LRU cache (key: SHA256 xml hash) -> model
    private readonly Dictionary<string, LinkedListNode<(string Key, BpmnModel Model)>> _cacheIndex = new();
    private readonly LinkedList<(string Key, BpmnModel Model)> _lru = new();
    private readonly object _cacheLock = new();

    public BpmnParser() : this(new BpmnParserOptions()) { }
    public BpmnParser(BpmnParserOptions options) => _options = options;

    private BpmnModel? TryGetCached(string xml)
    {
        if (_options.CacheSize <= 0) return null;
        var key = Hash(xml);
        lock (_cacheLock)
        {
            if (_cacheIndex.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Model;
            }
        }
        return null;
    }

    private void Cache(string xml, BpmnModel model)
    {
        if (_options.CacheSize <= 0) return;
        var key = Hash(xml);
        lock (_cacheLock)
        {
            if (_cacheIndex.ContainsKey(key)) return;
            var node = new LinkedListNode<(string, BpmnModel)>((key, model));
            _lru.AddFirst(node);
            _cacheIndex[key] = node;
            while (_lru.Count > _options.CacheSize)
            {
                var last = _lru.Last!; _lru.RemoveLast(); _cacheIndex.Remove(last.Value.Key);
            }
        }
    }

    private static string Hash(string xml)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(xml));
        return Convert.ToHexString(bytes);
    }

    public Task<BpmnModel> ParseAsync(string xml, CancellationToken cancellationToken = default)
    {
        if (TryGetCached(xml) is { } cached) return Task.FromResult(cached);

        var diagnostics = new List<string>();
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var ns = doc.Root!.Name.Namespace;
        var process = doc.Descendants(ns + "process").FirstOrDefault();
        if (process == null)
        {
            if (_options.StrictValidation) diagnostics.Add("No <process> element");
            var empty = new BpmnModel(string.Empty, Array.Empty<BpmnEvent>(), Array.Empty<BpmnGateway>(), Array.Empty<BpmnSubprocess>(), Array.Empty<BpmnSequenceFlow>(), Array.Empty<BpmnTask>(), Array.Empty<BpmnDataObject>(), Array.Empty<BpmnDataObjectReference>(), Array.Empty<BpmnDataStore>(), Array.Empty<BpmnDataStoreReference>(), Array.Empty<BpmnProperty>(), Array.Empty<BpmnActivityIo>(), Array.Empty<BpmnMessage>(), Array.Empty<BpmnSignal>(), Array.Empty<BpmnError>(), Array.Empty<BpmnEscalation>(), diagnostics);
            Cache(xml, empty);
            return Task.FromResult(empty);
        }
        var pid = process.Attribute("id")?.Value ?? string.Empty;

        var messages = doc.Descendants(ns + "message").Select(m => new BpmnMessage(m.Attribute("id")?.Value ?? string.Empty, m.Attribute("name")?.Value)).Where(m => !string.IsNullOrEmpty(m.Id)).ToList();
        var signals = doc.Descendants(ns + "signal").Select(s => new BpmnSignal(s.Attribute("id")?.Value ?? string.Empty, s.Attribute("name")?.Value)).Where(s => !string.IsNullOrEmpty(s.Id)).ToList();
        var errors = doc.Descendants(ns + "error").Select(e => new BpmnError(e.Attribute("id")?.Value ?? string.Empty, e.Attribute("name")?.Value, e.Attribute("errorCode")?.Value)).Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
        var escalations = doc.Descendants(ns + "escalation").Select(e => new BpmnEscalation(e.Attribute("id")?.Value ?? string.Empty, e.Attribute("name")?.Value, e.Attribute("escalationCode")?.Value)).Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
        var msgIds = new HashSet<string>(messages.Select(m => m.Id));
        var sigIds = new HashSet<string>(signals.Select(s => s.Id));
        var errIds = new HashSet<string>(errors.Select(e => e.Id));
        var escIds = new HashSet<string>(escalations.Select(e => e.Id));

        var gatewaysRaw = process.Elements().Where(e => e.Name.LocalName.EndsWith("Gateway"))
            .Select(g => new { Id = g.Attribute("id")?.Value ?? string.Empty, Type = g.Name.LocalName, DefaultId = g.Attribute("default")?.Value })
            .ToList();
        var defaultIds = new HashSet<string>(gatewaysRaw.Select(g => g.DefaultId).Where(v => !string.IsNullOrWhiteSpace(v))!);

        var subprocessStack = new Stack<string>();
        var events = new List<BpmnEvent>();
        var gateways = new List<BpmnGateway>();
        var subprocesses = new List<BpmnSubprocess>();
        var flows = new List<BpmnSequenceFlow>();
        var tasks = new List<BpmnTask>();
        var dataObjects = new List<BpmnDataObject>();
        var dataObjectRefs = new List<BpmnDataObjectReference>();
        var dataStores = new List<BpmnDataStore>();
        var dataStoreRefs = new List<BpmnDataStoreReference>();
        var properties = new List<BpmnProperty>();
        var activityIo = new List<BpmnActivityIo>();
        var participants = new List<BpmnParticipant>();
        var lanes = new List<BpmnLane>();
        var messageFlows = new List<BpmnMessageFlow>();
        var textAnnotations = new List<BpmnTextAnnotation>();
        var associationArtifacts = new List<BpmnAssociationArtifact>();
        var groups = new List<BpmnGroup>();

        var flowNodeIds = new HashSet<string>();
        var tasksOrSubs = new HashSet<string>();
        var idIndex = new HashSet<string>();
        var pendingMiConflicts = new HashSet<string>();

        Dictionary<string,string>? ExtractExtensions(XElement el)
        {
            if (!_options.PreserveUnknownExtensions) return null;
            var extParent = el.Element(ns + "extensionElements") ?? el.Element("extensionElements");
            if (extParent == null) return null;
            var dict = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            void Harvest(XElement node)
            {
                foreach (var attr in node.Attributes())
                {
                    var key = $"{node.Name}:{attr.Name.LocalName}";
                    dict[key] = attr.Value;
                }
                if (!node.HasAttributes && node != extParent)
                {
                    var key = $"{node.Name}:__present";
                    dict[key] = "true";
                }
                foreach (var child in node.Elements()) Harvest(child);
            }
            foreach (var top in extParent.Elements()) Harvest(top);
            return dict.Count == 0 ? null : dict;
        }

        void Walk(XElement parent)
        {
            foreach (var el in parent.Elements())
            {
                var local = el.Name.LocalName;
                var id = el.Attribute("id")?.Value ?? string.Empty;
                string? currentSub = subprocessStack.Count > 0 ? subprocessStack.Peek() : null;
                if (!string.IsNullOrEmpty(id))
                {
                    if (!idIndex.Add(id) && _options.StrictValidation) diagnostics.Add($"Duplicate ID: {id}");
                }
                Dictionary<string,string>? ext = ExtractExtensions(el);
                switch (local)
                {
                    case "subProcess":
                    case "adHocSubProcess":
                        var isEvent = el.Attribute("triggeredByEvent")?.Value == "true";
                        var isTx = el.Attribute("transaction")?.Value == "true";
                        var loop = ParseLoopWithConflict(el, ns, pendingMiConflicts);
                        subprocesses.Add(new BpmnSubprocess(id, isEvent, isTx, loop.loop, currentSub, ext));
                        if (loop.conflict) pendingMiConflicts.Add(id);
                        flowNodeIds.Add(id); tasksOrSubs.Add(id);
                        subprocessStack.Push(id); Walk(el); subprocessStack.Pop();
                        break;
                    case "startEvent":
                    case "endEvent":
                    case "intermediateCatchEvent":
                    case "intermediateThrowEvent":
                    case "boundaryEvent":
                        var defs = ParseEventDefinitions(el, ns);
                        events.Add(new BpmnEvent(id, local, defs, currentSub, ext));
                        flowNodeIds.Add(id);
                        break;
                    case var _ when local.EndsWith("Task") || local == "callActivity":
                        tasks.Add(new BpmnTask(id, local, currentSub, ext));
                        flowNodeIds.Add(id); tasksOrSubs.Add(id);
                        break;
                    case var _ when local.EndsWith("Gateway"):
                        flowNodeIds.Add(id);
                        gateways.Add(new BpmnGateway(id, local, gatewaysRaw.FirstOrDefault(g=>g.Id==id)?.DefaultId, currentSub, ext));
                        break;
                    case "sequenceFlow":
                        int? priority = null;
                        var prAttr = el.Attribute(XName.Get("priority", "http://vertexbpmn.io/schema/1.0"))
                                   ?? el.Attribute(XName.Get("priority", "http://camunda.org/schema/1.0/bpmn"))
                                   ?? el.Attribute("priority");
                        if (prAttr != null && int.TryParse(prAttr.Value, out var pVal)) priority = pVal;
                        flows.Add(new BpmnSequenceFlow(id,
                            el.Attribute("sourceRef")?.Value ?? string.Empty,
                            el.Attribute("targetRef")?.Value ?? string.Empty,
                            defaultIds.Contains(id),
                            (el.Element(ns + "conditionExpression")?.Value ?? el.Element("conditionExpression")?.Value)?.Trim(),
                            currentSub, ext, priority));
                        break;
                    case "dataObject":
                        dataObjects.Add(new BpmnDataObject(id, el.Attribute("name")?.Value));
                        break;
                    case "dataObjectReference":
                        dataObjectRefs.Add(new BpmnDataObjectReference(id, el.Attribute("dataObjectRef")?.Value ?? string.Empty));
                        break;
                    case "dataStore":
                        dataStores.Add(new BpmnDataStore(id, el.Attribute("name")?.Value));
                        break;
                    case "dataStoreReference":
                        dataStoreRefs.Add(new BpmnDataStoreReference(id, el.Attribute("dataStoreRef")?.Value ?? string.Empty));
                        break;
                    case "property":
                        properties.Add(new BpmnProperty(id, el.Attribute("name")?.Value));
                        break;
                }
            }
        }

        Walk(process);

        // Boundary event attachment validation (restored)
        if (_options.StrictValidation)
        {
            foreach (var boundary in process.Descendants(ns + "boundaryEvent"))
            {
                var bid = boundary.Attribute("id")?.Value;
                var attachedTo = boundary.Attribute("attachedToRef")?.Value;
                if (attachedTo != null && !tasksOrSubs.Contains(attachedTo))
                {
                    diagnostics.Add($"Boundary event {bid} attachedToRef {attachedTo} not found");
                }
            }
        }

        // Parse Activity IO specifications (tasks & subprocesses) – restored after Phase I refactor
        foreach (var act in process.Descendants())
        {
            var local = act.Name.LocalName;
            if (!(local.EndsWith("Task") || local == "subProcess" || local == "adHocSubProcess")) continue;
            var ioSpec = act.Element(ns + "ioSpecification") ?? act.Element("ioSpecification");
            if (ioSpec == null) continue;
            var inputs = ioSpec.Elements().Where(e => e.Name.LocalName == "dataInput")
                .Select(e => new BpmnDataInput(e.Attribute("id")?.Value ?? string.Empty, e.Attribute("name")?.Value)).ToList();
            var outputs = ioSpec.Elements().Where(e => e.Name.LocalName == "dataOutput")
                .Select(e => new BpmnDataOutput(e.Attribute("id")?.Value ?? string.Empty, e.Attribute("name")?.Value)).ToList();
            var inAssocs = act.Elements().Where(e => e.Name.LocalName == "dataInputAssociation")
                .Select(e => new BpmnDataAssociation(
                    e.Elements().FirstOrDefault(x => x.Name.LocalName == "sourceRef")?.Value ?? string.Empty,
                    e.Elements().FirstOrDefault(x => x.Name.LocalName == "targetRef")?.Value ?? string.Empty)).ToList();
            var outAssocs = act.Elements().Where(e => e.Name.LocalName == "dataOutputAssociation")
                .Select(e => new BpmnDataAssociation(
                    e.Elements().FirstOrDefault(x => x.Name.LocalName == "sourceRef")?.Value ?? string.Empty,
                    e.Elements().FirstOrDefault(x => x.Name.LocalName == "targetRef")?.Value ?? string.Empty)).ToList();
            activityIo.Add(new BpmnActivityIo(act.Attribute("id")?.Value ?? string.Empty, inputs, outputs, inAssocs, outAssocs));
        }

        // Rebuild containment (Phase G) ensuring non-null child collections
        if (subprocesses.Count > 0)
        {
            var flowNodeByParent = new Dictionary<string, List<string>>();
            void AddNodeToParent(string? subId, string nodeId)
            {
                if (string.IsNullOrEmpty(subId)) return;
                if (!flowNodeByParent.TryGetValue(subId, out var list)) { list = new List<string>(); flowNodeByParent[subId] = list; }
                list.Add(nodeId);
            }
            foreach (var e in events) AddNodeToParent(e.SubprocessId, e.Id);
            foreach (var t in tasks) AddNodeToParent(t.SubprocessId, t.Id);
            foreach (var g in gateways) AddNodeToParent(g.SubprocessId, g.Id);
            foreach (var sp in subprocesses.Where(s => s.SubprocessId != null)) AddNodeToParent(sp.SubprocessId, sp.Id);
            var seqByParent = new Dictionary<string, List<string>>();
            foreach (var f in flows)
            {
                if (string.IsNullOrEmpty(f.SubprocessId)) continue;
                if (!seqByParent.TryGetValue(f.SubprocessId, out var list)) { list = new List<string>(); seqByParent[f.SubprocessId] = list; }
                list.Add(f.Id);
            }
            for (int i = 0; i < subprocesses.Count; i++)
            {
                var sp = subprocesses[i];
                flowNodeByParent.TryGetValue(sp.Id, out var nodeChildren);
                seqByParent.TryGetValue(sp.Id, out var flowChildren);
                subprocesses[i] = sp with
                {
                    ChildFlowNodeIds = (nodeChildren ?? new List<string>()).AsReadOnly(),
                    ChildSequenceFlowIds = (flowChildren ?? new List<string>()).AsReadOnly()
                };
            }
        }

        // Collaboration (participants, message flows) at definitions level
        var collaboration = doc.Descendants(ns + "collaboration").FirstOrDefault();
        if (collaboration != null)
        {
            foreach (var part in collaboration.Elements(ns + "participant"))
            {
                var pidRef = part.Attribute("processRef")?.Value;
                participants.Add(new BpmnParticipant(part.Attribute("id")?.Value ?? string.Empty, part.Attribute("name")?.Value, pidRef));
            }
            foreach (var mf in collaboration.Elements(ns + "messageFlow"))
            {
                messageFlows.Add(new BpmnMessageFlow(mf.Attribute("id")?.Value ?? string.Empty,
                    mf.Attribute("sourceRef")?.Value ?? string.Empty,
                    mf.Attribute("targetRef")?.Value ?? string.Empty,
                    mf.Attribute("name")?.Value));
            }
        }
        // Lanes
        foreach (var laneSet in process.Elements(ns + "laneSet"))
        {
            foreach (var lane in laneSet.Elements(ns + "lane"))
            {
                var flowNodeRefs = lane.Elements(ns + "flowNodeRef").Select(e => e.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                lanes.Add(new BpmnLane(lane.Attribute("id")?.Value ?? string.Empty, lane.Attribute("name")?.Value, flowNodeRefs));
            }
        }
        // Artifacts inside process
        foreach (var ta in process.Elements(ns + "textAnnotation"))
        {
            var text = ta.Element(ns + "text")?.Value ?? ta.Element("text")?.Value;
            textAnnotations.Add(new BpmnTextAnnotation(ta.Attribute("id")?.Value ?? string.Empty, text));
        }
        foreach (var assoc in process.Elements(ns + "association"))
        {
            associationArtifacts.Add(new BpmnAssociationArtifact(assoc.Attribute("id")?.Value ?? string.Empty,
                assoc.Attribute("sourceRef")?.Value ?? string.Empty,
                assoc.Attribute("targetRef")?.Value ?? string.Empty,
                assoc.Attribute("associationDirection")?.Value));
        }
        foreach (var grp in process.Elements(ns + "group"))
        {
            groups.Add(new BpmnGroup(grp.Attribute("id")?.Value ?? string.Empty, grp.Attribute("categoryValueRef")?.Value));
        }

        // Validations (conditionally emitted)
        var outgoing = flows.GroupBy(f => f.SourceRef).ToDictionary(g => g.Key, g => g.Count());
        if (_options.StrictValidation)
        {
            foreach (var f in flows)
            {
                if (!flowNodeIds.Contains(f.SourceRef) || !flowNodeIds.Contains(f.TargetRef))
                    diagnostics.Add($"SequenceFlow {f.Id} has invalid endpoints {f.SourceRef}->{f.TargetRef}");
                if (f.IsDefault && !string.IsNullOrWhiteSpace(f.ConditionExpression))
                    diagnostics.Add($"Default flow {f.Id} must not have a conditionExpression");
            }
            foreach (var gw in gateways)
                if (!outgoing.ContainsKey(gw.Id)) diagnostics.Add($"Gateway {gw.Id} has no outgoing sequenceFlow");
            if (!events.Any(e => e.Type == "startEvent")) diagnostics.Add("No startEvent found in process");
            foreach (var mi in pendingMiConflicts) diagnostics.Add($"Invalid multi-instance: cardinality and collection both present on {mi}");
            var transactionIds = new HashSet<string>(subprocesses.Where(s => s.IsTransaction).Select(s => s.Id));
            foreach (var ev in events.Where(e => e.Definitions.OfType<CancelEventDefinition>().Any()))
                if (ev.SubprocessId == null || !transactionIds.Contains(ev.SubprocessId)) diagnostics.Add($"Cancel end event {ev.Id} outside transaction subprocess");
            foreach (var ev in events)
            {
                foreach (var md in ev.Definitions.OfType<MessageEventDefinition>()) if (!string.IsNullOrWhiteSpace(md.MessageRef) && !msgIds.Contains(md.MessageRef)) diagnostics.Add($"Unknown messageRef {md.MessageRef} in event {ev.Id}");
                foreach (var sd in ev.Definitions.OfType<SignalEventDefinition>()) if (!string.IsNullOrWhiteSpace(sd.SignalRef) && !sigIds.Contains(sd.SignalRef)) diagnostics.Add($"Unknown signalRef {sd.SignalRef} in event {ev.Id}");
                foreach (var ed in ev.Definitions.OfType<ErrorEventDefinition>()) if (!string.IsNullOrWhiteSpace(ed.ErrorRef) && !errIds.Contains(ed.ErrorRef)) diagnostics.Add($"Unknown errorRef {ed.ErrorRef} in event {ev.Id}");
                foreach (var escd in ev.Definitions.OfType<EscalationEventDefinition>()) if (!string.IsNullOrWhiteSpace(escd.EscalationRef) && !escIds.Contains(escd.EscalationRef)) diagnostics.Add($"Unknown escalationRef {escd.EscalationRef} in event {ev.Id}");
            }
            // Link pairing
            var linkByName = new Dictionary<string,(int Throw,int Catch)>();
            foreach (var ev in events)
            {
                foreach (var link in ev.Definitions.OfType<LinkEventDefinition>())
                {
                    if (string.IsNullOrWhiteSpace(link.Name)) continue;
                    linkByName.TryGetValue(link.Name, out var counts);
                    if (ev.Type == "intermediateThrowEvent") counts.Throw++; else if (ev.Type == "intermediateCatchEvent") counts.Catch++;
                    linkByName[link.Name] = counts;
                }
            }
            foreach (var (name, counts) in linkByName)
            {
                if (counts.Throw == 0 || counts.Catch == 0) diagnostics.Add($"Unmatched link event name {name}");
                if (counts.Throw > 1) diagnostics.Add($"Multiple throw link events for name {name}");
                if (counts.Catch > 1) diagnostics.Add($"Multiple catch link events for name {name}");
            }
        }

        List<BpmnShape>? shapes = null;
        List<BpmnEdge>? edges = null;
        if (_options.ParseDiagramInterchange)
        {
            // BPMN DI namespaces
            var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
            var omgdc = (XNamespace)"http://www.omg.org/spec/DD/20100524/DC";
            var omgdi = (XNamespace)"http://www.omg.org/spec/DD/20100524/DI";
            shapes = new List<BpmnShape>();
            edges = new List<BpmnEdge>();
            foreach (var shape in doc.Descendants(bpmndi + "BPMNShape"))
            {
                var id = shape.Attribute("id")?.Value ?? string.Empty;
                var bpmnElement = shape.Attribute("bpmnElement")?.Value ?? string.Empty;
                var bounds = shape.Element(omgdc + "Bounds");
                if (bounds != null &&
                    double.TryParse(bounds.Attribute("x")?.Value, out var x) &&
                    double.TryParse(bounds.Attribute("y")?.Value, out var y) &&
                    double.TryParse(bounds.Attribute("width")?.Value, out var w) &&
                    double.TryParse(bounds.Attribute("height")?.Value, out var h))
                {
                    shapes.Add(new BpmnShape(id, bpmnElement, x, y, w, h));
                }
            }
            foreach (var edge in doc.Descendants(bpmndi + "BPMNEdge"))
            {
                var id = edge.Attribute("id")?.Value ?? string.Empty;
                var bpmnElement = edge.Attribute("bpmnElement")?.Value ?? string.Empty;
                var wp = new List<(double X,double Y)>();
                foreach (var waypoint in edge.Elements(omgdi + "waypoint"))
                {
                    if (double.TryParse(waypoint.Attribute("x")?.Value, out var wx) && double.TryParse(waypoint.Attribute("y")?.Value, out var wy))
                        wp.Add((wx, wy));
                }
                edges.Add(new BpmnEdge(id, bpmnElement, wp));
            }
        }
        var activities =  tasks.Cast<object>().Concat(subprocesses);
        var model = new BpmnModel(pid, events, gateways, subprocesses, flows, tasks, dataObjects, dataObjectRefs, dataStores, dataStoreRefs, properties, activityIo, messages, signals, errors, escalations, diagnostics, shapes, edges, participants, lanes, messageFlows, textAnnotations, associationArtifacts, groups, Activities: activities);

        Cache(xml, model);
        return Task.FromResult(model);
    }

    public string Serialize(BpmnModel model)
    {
       return new BpmnSerializer().Serialize(model);
    }

    private static (LoopCharacteristics? loop, bool conflict) ParseLoopWithConflict(XElement sp, XNamespace ns, HashSet<string> conflictSet)
    {
        var mi = sp.Element(ns + "multiInstanceLoopCharacteristics") ?? sp.Element("multiInstanceLoopCharacteristics");
        if (mi != null)
        {
            bool isSeq = mi.Attribute("isSequential")?.Value == "true";
            int? card = null; var cardText = mi.Element(ns + "loopCardinality")?.Value ?? mi.Element("loopCardinality")?.Value; if (int.TryParse(cardText, out var cParsed)) card = cParsed;
            var camundaCollection = mi.Attribute(XName.Get("collection", "http://camunda.org/schema/1.0/bpmn"))?.Value;
            var zeebeCollection = mi.Element(XName.Get("inputCollection", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var collectionRaw = camundaCollection ?? zeebeCollection;
            var camundaElementVar = mi.Attribute(XName.Get("elementVariable", "http://camunda.org/schema/1.0/bpmn"))?.Value;
            var zeebeInputElement = mi.Element(XName.Get("inputElement", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var zeebeOutputElement = mi.Element(XName.Get("outputElement", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var elementVar = camundaElementVar ?? zeebeInputElement ?? zeebeOutputElement;
            var completion = mi.Element(ns + "completionCondition")?.Value ?? mi.Element("completionCondition")?.Value;
            bool conflict = !string.IsNullOrWhiteSpace(collectionRaw) && card.HasValue;
            if (!string.IsNullOrWhiteSpace(collectionRaw)) card = null;
            var loop = new MultiInstanceLoopCharacteristics(isSeq, card, collectionRaw, elementVar, completion, zeebeInputElement, zeebeOutputElement);
            return (loop, conflict);
        }
        var std = sp.Element(ns + "standardLoopCharacteristics") ?? sp.Element("standardLoopCharacteristics");
        if (std != null)
        {
            var loopCond = std.Element(ns + "loopCondition")?.Value ?? std.Element("loopCondition")?.Value;
            bool testBefore = std.Attribute("testBefore")?.Value == "true";
            int? loopMax = null; if (int.TryParse(std.Attribute("loopMaximum")?.Value, out var lm)) loopMax = lm;
            return (new StandardLoopCharacteristics(loopCond, testBefore, loopMax), false);
        }
        return (null, false);
    }

    private static IReadOnlyList<EventDefinition> ParseEventDefinitions(XElement evt, XNamespace ns)
    {
        var list = new List<EventDefinition>();
        foreach (var defElem in evt.Elements())
        {
            switch (defElem.Name.LocalName)
            {
                case "timerEventDefinition":
                    list.Add(new TimerEventDefinition(
                        defElem.Element(ns + "timeDate")?.Value ?? defElem.Element("timeDate")?.Value,
                        defElem.Element(ns + "timeDuration")?.Value ?? defElem.Element("timeDuration")?.Value,
                        defElem.Element(ns + "timeCycle")?.Value ?? defElem.Element("timeCycle")?.Value));
                    break;
                case "messageEventDefinition":
                    list.Add(new MessageEventDefinition(defElem.Attribute("messageRef")?.Value ?? string.Empty, defElem.Attribute("correlationKey")?.Value));
                    break;
                case "signalEventDefinition":
                    list.Add(new SignalEventDefinition(defElem.Attribute("signalRef")?.Value ?? string.Empty));
                    break;
                case "errorEventDefinition":
                    list.Add(new ErrorEventDefinition(defElem.Attribute("errorRef")?.Value ?? string.Empty));
                    break;
                case "conditionalEventDefinition":
                    var cond = defElem.Element(ns + "conditionExpression")?.Value ?? defElem.Element("conditionExpression")?.Value ?? string.Empty;
                    list.Add(new ConditionalEventDefinition(cond));
                    break;
                case "terminateEventDefinition":
                    list.Add(new TerminateEventDefinition());
                    break;
                case "cancelEventDefinition":
                    list.Add(new CancelEventDefinition());
                    break;
                case "compensateEventDefinition":
                    list.Add(new CompensationEventDefinition(defElem.Attribute("activityRef")?.Value));
                    break;
                case "escalationEventDefinition":
                    list.Add(new EscalationEventDefinition(defElem.Attribute("escalationRef")?.Value ?? string.Empty));
                    break;
                case "linkEventDefinition":
                    list.Add(new LinkEventDefinition(defElem.Attribute("name")?.Value ?? string.Empty));
                    break;
            }
        }
        return list;
    }
}
