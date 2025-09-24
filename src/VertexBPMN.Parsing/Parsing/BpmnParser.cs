//See docs/ROUNDTRIP_STRICT_PLAN.md

using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Parsing;

public class BpmnParser : IBpmnParser
{
    private readonly BpmnParserOptions _options;
    private readonly Dictionary<string, LinkedListNode<(string Key, BpmnModel Model)>> _cacheIndex = new();
    private readonly LinkedList<(string Key, BpmnModel Model)> _lru = new();
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, string> _idPool = new(StringComparer.Ordinal);

    private string Intern(string s)
    {
        if (!_options.InternIds) return s;
        if (s.Length == 0) return s;
        if (_idPool.TryGetValue(s, out var existing)) return existing;
        _idPool[s] = s;
        return s;
    }

    public BpmnParser() : this(new BpmnParserOptions()) { }
    public BpmnParser(BpmnParserOptions options) { _options = options; }

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
        var strict = _options.RoundtripMode == BpmnRoundtripMode.Strict;
        var rawDefinitionsAttr = strict ? new Dictionary<string,string>(StringComparer.Ordinal) : null;
        var rawProcessAttr = strict ? new Dictionary<string,string>(StringComparer.Ordinal) : null;
        var rawIncoming = strict ? new Dictionary<string, List<string>>() : null;
        var rawOutgoing = strict ? new Dictionary<string, List<string>>() : null;
        var rawCond = strict ? new Dictionary<string,(string Raw,bool WasCData)>() : null;
        var rawExtensions = strict ? new Dictionary<string,XElement>() : null;
        var rawEvDefs = strict ? new Dictionary<string, List<XElement>>() : null;
        var namespacePrefixes = strict ? new List<NamespacePrefix>() : null;
        var elementsMetadata = strict ? new Dictionary<string, ElementMetadata>() : null;
        var rawDocumentation = strict ? new Dictionary<string, List<XElement>>() : null;
        var rawGlobalElements = strict ? new List<XElement>() : null;
        var rawArtifacts = strict ? new List<XElement>() : null;
        var rawLanes = strict ? new List<XElement>() : null;
        // NEW Phase A captures (zero-break additive):
        var rawMultiInstance = strict ? new Dictionary<string, XElement>() : null; // original loop characteristics node
        var priorityAttrNs = strict ? new Dictionary<string, string>(StringComparer.Ordinal) : null; // priority attribute namespace per sequenceFlow
        var flowNodeAttributes = strict ? new Dictionary<string, IReadOnlyDictionary<string,string>>(StringComparer.Ordinal) : null; // attribute snapshot per flow node / sequenceFlow

        XElement? diRoot = null;
        var diagnostics = new List<string>();
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var root = doc.Root!; var ns = root.Name.Namespace;
        if (strict)
        {
            foreach (var attr in root.Attributes()) if (attr.IsNamespaceDeclaration)
            {
                var prefix = (attr.Name.Namespace==XNamespace.None && attr.Name.LocalName=="xmlns")? string.Empty: attr.Name.LocalName;
                namespacePrefixes!.Add(new NamespacePrefix(prefix, attr.Value, true));
            }
            foreach (var a in root.Attributes()) if(!a.IsNamespaceDeclaration) rawDefinitionsAttr![a.Name.ToString()] = a.Value;
        }

        var process = doc.Descendants(ns + "process").FirstOrDefault();
        if (process == null)
        {
            if (_options.StrictValidation) diagnostics.Add("No <process> element");
            var empty = new BpmnModel(string.Empty, Array.Empty<BpmnEvent>(), Array.Empty<BpmnGateway>(), Array.Empty<BpmnSubprocess>(), Array.Empty<BpmnSequenceFlow>(), Array.Empty<BpmnTask>(), Array.Empty<BpmnDataObject>(), Array.Empty<BpmnDataObjectReference>(), Array.Empty<BpmnDataStore>(), Array.Empty<BpmnDataStoreReference>(), Array.Empty<BpmnProperty>(), Array.Empty<BpmnActivityIo>(), Array.Empty<BpmnMessage>(), Array.Empty<BpmnSignal>(), Array.Empty<BpmnError>(), Array.Empty<BpmnEscalation>(), diagnostics, RawMetadata: strict ? new BpmnRawMetadata(rawDefinitionsAttr, rawProcessAttr) : null);
            Cache(xml, empty); return Task.FromResult(empty);
        }
        var pid = Intern(process.Attribute("id")?.Value ?? string.Empty);
        if (strict)
        {
            foreach (var a in process.Attributes()) rawProcessAttr![a.Name.ToString()] = a.Value;
            var docNodes = process.Elements(ns+"documentation").Concat(process.Elements("documentation"));
            foreach(var dn in docNodes)
            {
                if(!rawDocumentation!.TryGetValue("__process", out var list)) { list=new(); rawDocumentation["__process"]=list; }
                list.Add(new XElement(dn));
            }
        }

        // global elements capture
        var messages = doc.Descendants(ns + "message").ToList(); var signals = doc.Descendants(ns + "signal").ToList(); var errors = doc.Descendants(ns + "error").ToList(); var escalations = doc.Descendants(ns + "escalation").ToList(); if(strict){ foreach(var g in messages.Concat(signals).Concat(errors).Concat(escalations)) rawGlobalElements!.Add(new XElement(g)); }
        var messageModels = messages.Select(m=> new BpmnMessage(Intern(m.Attribute("id")?.Value ?? string.Empty), m.Attribute("name")?.Value)).Where(m=>!string.IsNullOrEmpty(m.Id)).ToList();
        var signalModels = signals.Select(s=> new BpmnSignal(Intern(s.Attribute("id")?.Value ?? string.Empty), s.Attribute("name")?.Value)).Where(s=>!string.IsNullOrEmpty(s.Id)).ToList();
        var errorModels = errors.Select(e=> new BpmnError(Intern(e.Attribute("id")?.Value ?? string.Empty), e.Attribute("name")?.Value, e.Attribute("errorCode")?.Value)).Where(e=>!string.IsNullOrEmpty(e.Id)).ToList();
        var escalationModels = escalations.Select(e=> new BpmnEscalation(Intern(e.Attribute("id")?.Value ?? string.Empty), e.Attribute("name")?.Value, e.Attribute("escalationCode")?.Value)).Where(e=>!string.IsNullOrEmpty(e.Id)).ToList();

        var gatewaysRaw = process.Elements().Where(e=>e.Name.LocalName.EndsWith("Gateway")).Select(g=> new { Id = Intern(g.Attribute("id")?.Value ?? string.Empty), Type=g.Name.LocalName, DefaultId = g.Attribute("default")?.Value}).ToList();
        var defaultIds = new HashSet<string>(gatewaysRaw.Select(g=>g.DefaultId).Where(v=>!string.IsNullOrWhiteSpace(v))!);
        var subprocessStack = new Stack<string>();
        var events = new List<BpmnEvent>(); var gateways = new List<BpmnGateway>(); var subprocesses = new List<BpmnSubprocess>(); var flows = new List<BpmnSequenceFlow>(); var tasks = new List<BpmnTask>(); var dataObjects=new List<BpmnDataObject>(); var dataObjectRefs=new List<BpmnDataObjectReference>(); var dataStores=new List<BpmnDataStore>(); var dataStoreRefs=new List<BpmnDataStoreReference>(); var properties=new List<BpmnProperty>(); var activityIo=new List<BpmnActivityIo>(); var participants=new List<BpmnParticipant>(); var lanes=new List<BpmnLane>(); var messageFlows=new List<BpmnMessageFlow>(); var textAnnotations=new List<BpmnTextAnnotation>(); var associationArtifacts=new List<BpmnAssociationArtifact>(); var groups=new List<BpmnGroup>();
        var flowNodeIds = new HashSet<string>(); var idIndex=new HashSet<string>(); var pendingMiConflicts=new HashSet<string>();
        var transactionIds = new HashSet<string>(); var boundaryEvents = new List<(string Id,string? Attached)>(); var linkThrowCounts = new Dictionary<string,int>(StringComparer.Ordinal); var linkCatchNames = new HashSet<string>(StringComparer.Ordinal);
        int orderCounter = 0;

        Dictionary<string,string>? ExtractExtensions(XElement el){ if(!_options.PreserveUnknownExtensions) return null; var extParent= el.Element(ns+"extensionElements") ?? el.Element("extensionElements"); if(extParent==null) return null; var dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); if(strict){ var ownerId= el.Attribute("id")?.Value; if(!string.IsNullOrEmpty(ownerId)) rawExtensions![ownerId]= new XElement(extParent);} void Harvest(XElement node){ foreach(var attr in node.Attributes()){ var key=$"{node.Name}:{attr.Name.LocalName}"; dict[key]=attr.Value;} if(!node.HasAttributes && node!=extParent){ var key=$"{node.Name}:__present"; dict[key]="true";} foreach(var child in node.Elements()) Harvest(child);} foreach(var top in extParent.Elements()) Harvest(top); return dict.Count==0? null: dict; }

        void CaptureElementMeta(XElement el,string id,bool hadCamundaCollection=false,bool hadZeebeInputCollection=false,bool hadLoopCardinality=false,bool hadCamundaElementVar=false,bool hadZeebeInputElement=false,bool hadZeebeOutputElement=false){ if(!strict||string.IsNullOrEmpty(id)) return; var attrDict=new Dictionary<string,string>(StringComparer.Ordinal); foreach(var a in el.Attributes()){ if(a.IsNamespaceDeclaration) continue; attrDict[a.Name.ToString()]=a.Value; } elementsMetadata![id]= new ElementMetadata(orderCounter, el.Name.LocalName, attrDict, hadCamundaCollection, hadZeebeInputCollection, hadLoopCardinality, hadCamundaElementVar, hadZeebeInputElement, hadZeebeOutputElement); if(flowNodeAttributes!=null) flowNodeAttributes[id]=attrDict; var docs = el.Elements(ns + "documentation").Concat(el.Elements("documentation")).ToList(); if (docs.Count > 0){ if (!rawDocumentation!.TryGetValue(id, out var list)) { list = new List<XElement>(); rawDocumentation[id] = list; } foreach (var d in docs) list.Add(new XElement(d)); } }

        (LoopCharacteristics? loop, bool conflict) ParseLoopLocal(XElement sp){ var res= ParseLoopWithConflict(sp, ns, pendingMiConflicts); if(res.conflict) pendingMiConflicts.Add(sp.Attribute("id")?.Value ?? ""); return res; }

        void Walk(XElement parent){ foreach(var el in parent.Elements()){ cancellationToken.ThrowIfCancellationRequested(); orderCounter++; var local= el.Name.LocalName; var id= Intern(el.Attribute("id")?.Value ?? string.Empty); string? currentSub = subprocessStack.Count>0? subprocessStack.Peek(): null; if(!string.IsNullOrEmpty(id)){ if(!idIndex.Add(id) && _options.StrictValidation) diagnostics.Add($"Duplicate ID: {id}"); } Dictionary<string,string>? ext = ExtractExtensions(el); switch (local)
            {
                case "subProcess":
                case "adHocSubProcess":
                    var isEvent = el.Attribute("triggeredByEvent")?.Value=="true"; var isTx = el.Attribute("transaction")?.Value=="true" || local=="transaction"; var loopInfo = ParseLoopLocal(el); // capture raw loop node
                    if(strict && !string.IsNullOrEmpty(id)) { var miNode = el.Element(ns+"multiInstanceLoopCharacteristics") ?? el.Element("multiInstanceLoopCharacteristics"); var stdNode = el.Element(ns+"standardLoopCharacteristics") ?? el.Element("standardLoopCharacteristics"); if(miNode!=null || stdNode!=null) rawMultiInstance![id]= new XElement(miNode ?? stdNode); }
                    subprocesses.Add(new BpmnSubprocess(id, isEvent, isTx, loopInfo.loop, currentSub, ext)); if(isTx && !string.IsNullOrEmpty(id)) transactionIds.Add(id); flowNodeIds.Add(id); CaptureElementMeta(el,id, el.Attribute(XName.Get("collection","http://camunda.org/schema/1.0/bpmn"))!=null, el.Element(XName.Get("inputCollection","http://zeebe.io/schema/zeebe/1.0"))!=null, el.Element(ns+"loopCardinality")!=null|| el.Element("loopCardinality")!=null, el.Attribute(XName.Get("elementVariable","http://camunda.org/schema/1.0/bpmn"))!=null, el.Element(XName.Get("inputElement","http://zeebe.io/schema/zeebe/1.0"))!=null, el.Element(XName.Get("outputElement","http://zeebe.io/schema/zeebe/1.0"))!=null); subprocessStack.Push(id); Walk(el); subprocessStack.Pop(); break;
                case "startEvent":
                case "endEvent":
                case "intermediateCatchEvent":
                case "intermediateThrowEvent":
                case "boundaryEvent":
                    var defs = ParseEventDefinitions(el, ns); events.Add(new BpmnEvent(id, local, defs, currentSub, ext)); flowNodeIds.Add(id); if(local=="boundaryEvent") boundaryEvents.Add((id, el.Attribute("attachedToRef")?.Value));
                    // link events tracking
                    foreach(var led in el.Elements()) if(led.Name.LocalName=="linkEventDefinition"){ var lname = led.Attribute("name")?.Value; if(!string.IsNullOrEmpty(lname)){ if(local=="intermediateThrowEvent"){ linkThrowCounts.TryGetValue(lname!, out var c); linkThrowCounts[lname!]= c+1; } else if(local=="intermediateCatchEvent"){ linkCatchNames.Add(lname!); } }}
                    if(strict){ var list = new List<XElement>(); foreach(var d in el.Elements()){ if(d.Name.LocalName.EndsWith("EventDefinition") || d.Name.LocalName.Contains("EventDefinition")) list.Add(new XElement(d)); } if(list.Count>0) rawEvDefs![id]=list; }
                    CaptureElementMeta(el,id); break;
                case var _ when local.EndsWith("Task") || local=="callActivity":
                    // capture raw loop for activities
                    if(strict && !string.IsNullOrEmpty(id)) { var miNodeT = el.Element(ns+"multiInstanceLoopCharacteristics") ?? el.Element("multiInstanceLoopCharacteristics"); var stdNodeT = el.Element(ns+"standardLoopCharacteristics") ?? el.Element("standardLoopCharacteristics"); if(miNodeT!=null || stdNodeT!=null) rawMultiInstance![id]= new XElement(miNodeT ?? stdNodeT); }
                    var task = new BpmnTask(id, local, currentSub, ext) { Name = el.Attribute("name")?.Value ?? string.Empty };
                    tasks.Add(task); flowNodeIds.Add(id);
                    // IO spec & associations
                    {
                        var ioSpec = el.Element(ns+"ioSpecification") ?? el.Element("ioSpecification"); if(ioSpec!=null){ var dataInputs = ioSpec.Elements(ns+"dataInput").Concat(ioSpec.Elements("dataInput")).Select(di=> { var did = Intern(di.Attribute("id")?.Value ?? string.Empty); if(string.IsNullOrEmpty(did)) return null; return new BpmnDataInput(did, di.Attribute("name")?.Value); }).OfType<BpmnDataInput>().ToList(); var dataOutputs = ioSpec.Elements(ns+"dataOutput").Concat(ioSpec.Elements("dataOutput")).Select(dout=> { var oid = Intern(dout.Attribute("id")?.Value ?? string.Empty); if(string.IsNullOrEmpty(oid)) return null; return new BpmnDataOutput(oid, dout.Attribute("name")?.Value); }).OfType<BpmnDataOutput>().ToList(); var inputAssociations = el.Elements(ns+"dataInputAssociation").Concat(el.Elements("dataInputAssociation")).Select(a=> { var src=a.Element(ns+"sourceRef")?.Value ?? a.Element("sourceRef")?.Value; var tgt=a.Element(ns+"targetRef")?.Value ?? a.Element("targetRef")?.Value; if(string.IsNullOrWhiteSpace(src)|| string.IsNullOrWhiteSpace(tgt)) return null; return new BpmnDataAssociation(Intern(src), Intern(tgt)); }).OfType<BpmnDataAssociation>().ToList(); var outputAssociations = el.Elements(ns+"dataOutputAssociation").Concat(el.Elements("dataOutputAssociation")).Select(a=> { var src=a.Element(ns+"sourceRef")?.Value ?? a.Element("sourceRef")?.Value; var tgt=a.Element(ns+"targetRef")?.Value ?? a.Element("targetRef")?.Value; if(string.IsNullOrWhiteSpace(src)|| string.IsNullOrWhiteSpace(tgt)) return null; return new BpmnDataAssociation(Intern(src), Intern(tgt)); }).OfType<BpmnDataAssociation>().ToList(); if(dataInputs.Count>0 || dataOutputs.Count>0 || inputAssociations.Count>0 || outputAssociations.Count>0) activityIo.Add(new BpmnActivityIo(id, dataInputs, dataOutputs, inputAssociations, outputAssociations)); }
                    }
                    CaptureElementMeta(el,id); break;
                case var _ when local.EndsWith("Gateway"):
                    flowNodeIds.Add(id); gateways.Add(new BpmnGateway(id, local, gatewaysRaw.FirstOrDefault(g=>g.Id==id)?.DefaultId, currentSub, ext)); CaptureElementMeta(el,id); break;
                case "laneSet":
                    if(strict) rawLanes!.Add(new XElement(el)); // keep laneSet structure
                    Walk(el); // recurse into lanes
                    continue; // prevent double attribute handling
                case "sequenceFlow":
                    int? priority=null; var prAttr= el.Attribute(XName.Get("priority","http://vertexbpmn.io/schema/1.0"))?? el.Attribute(XName.Get("priority","http://camunda.org/schema/1.0/bpmn"))?? el.Attribute("priority"); if(prAttr!=null && int.TryParse(prAttr.Value,out var pVal)) priority=pVal; if(strict && prAttr!=null && !string.IsNullOrEmpty(id)) priorityAttrNs![id]= prAttr.Name.NamespaceName; var condNode= el.Element(ns+"conditionExpression")?? el.Element("conditionExpression"); var condText = condNode?.Value?.Trim(); flows.Add(new BpmnSequenceFlow(id, Intern(el.Attribute("sourceRef")?.Value ?? string.Empty), Intern(el.Attribute("targetRef")?.Value ?? string.Empty), defaultIds.Contains(id), condText, currentSub, ext, priority)); if(strict && condNode!=null){ bool wasCData = condNode.Nodes().OfType<XCData>().Any(); rawCond![id]=(condNode.Value, wasCData);} CaptureElementMeta(el,id); break;
                case "dataObject": dataObjects.Add(new BpmnDataObject(id, el.Attribute("name")?.Value)); CaptureElementMeta(el,id); break;
                case "dataObjectReference": dataObjectRefs.Add(new BpmnDataObjectReference(id, Intern(el.Attribute("dataObjectRef")?.Value ?? string.Empty))); CaptureElementMeta(el,id); break;
                case "dataStore": dataStores.Add(new BpmnDataStore(id, el.Attribute("name")?.Value)); CaptureElementMeta(el,id); break;
                case "dataStoreReference": dataStoreRefs.Add(new BpmnDataStoreReference(id, Intern(el.Attribute("dataStoreRef")?.Value ?? string.Empty))); CaptureElementMeta(el,id); break;
                case "property": properties.Add(new BpmnProperty(id, el.Attribute("name")?.Value)); CaptureElementMeta(el,id); break;
                case "textAnnotation":
                    if (!_options.CaptureArtifacts && strict) { CaptureElementMeta(el, id); break; }
                    textAnnotations.Add(new BpmnTextAnnotation(id, el.Element(ns+"text")?.Value ?? el.Element("text")?.Value)); if(strict) rawArtifacts!.Add(new XElement(el)); CaptureElementMeta(el,id); break;
                case "group":
                    if (!_options.CaptureArtifacts && strict) { CaptureElementMeta(el, id); break; }
                    groups.Add(new BpmnGroup(id, el.Attribute("categoryValueRef")?.Value)); if(strict) rawArtifacts!.Add(new XElement(el)); CaptureElementMeta(el,id); break;
                case "association":
                    if (!_options.CaptureArtifacts && strict) { CaptureElementMeta(el, id); break; }
                    associationArtifacts.Add(new BpmnAssociationArtifact(id, el.Attribute("sourceRef")?.Value ?? string.Empty, el.Attribute("targetRef")?.Value ?? string.Empty, el.Attribute("associationDirection")?.Value)); if(strict) rawArtifacts!.Add(new XElement(el)); CaptureElementMeta(el,id); break;
                case "lane": if(strict) rawLanes!.Add(new XElement(el)); CaptureElementMeta(el,id); break;
            }

            // Phase B: missing id diagnostic for flow nodes / sequenceFlow / artifacts
            if (strict && string.IsNullOrEmpty(id))
            {
                // classify subset of elements requiring ids
                if (local is "userTask" or "serviceTask" or "task" || local.EndsWith("Task") ||
                    local is "startEvent" or "endEvent" or "intermediateCatchEvent" or "intermediateThrowEvent" or "boundaryEvent" ||
                    local.EndsWith("Gateway") || local is "sequenceFlow")
                {
                    diagnostics.Add($"Missing id on {local}");
                }
            }
        } }
        Walk(process);

        // Reference resolution diagnostics
        if (events.Count > 0)
        {
            var messageIds = new HashSet<string>(messageModels.Select(m => m.Id), StringComparer.Ordinal);
            var signalIds = new HashSet<string>(signalModels.Select(s => s.Id), StringComparer.Ordinal);
            var errorIds = new HashSet<string>(errorModels.Select(e => e.Id), StringComparer.Ordinal);
            var escalationIds = new HashSet<string>(escalationModels.Select(e => e.Id), StringComparer.Ordinal);
            foreach (var ev in events)
            {
                foreach (var def in ev.Definitions)
                {
                    switch (def)
                    {
                        case MessageEventDefinition m when !string.IsNullOrEmpty(m.MessageRef) && !messageIds.Contains(m.MessageRef): diagnostics.Add($"Unknown messageRef '{m.MessageRef}' at event {ev.Id}"); break;
                        case SignalEventDefinition s when !string.IsNullOrEmpty(s.SignalRef) && !signalIds.Contains(s.SignalRef): diagnostics.Add($"Unknown signalRef '{s.SignalRef}' at event {ev.Id}"); break;
                        case ErrorEventDefinition e when !string.IsNullOrEmpty(e.ErrorRef) && !errorIds.Contains(e.ErrorRef): diagnostics.Add($"Unknown errorRef '{e.ErrorRef}' at event {ev.Id}"); break;
                        case EscalationEventDefinition esc when !string.IsNullOrEmpty(esc.EscalationRef) && !escalationIds.Contains(esc.EscalationRef): diagnostics.Add($"Unknown escalationRef '{esc.EscalationRef}' at event {ev.Id}"); break;
                    }
                }
            }
        }

        // Additional validations
        foreach(var cid in pendingMiConflicts) if(!string.IsNullOrEmpty(cid)) diagnostics.Add($"multi-instance conflict on {cid}");
        foreach(var f in flows.Where(f=> f.IsDefault)) { bool hasCond = !string.IsNullOrWhiteSpace(f.ConditionExpression) || (rawCond!=null && rawCond.TryGetValue(f.Id, out var rc) && !string.IsNullOrEmpty(rc.Raw)); if(hasCond) diagnostics.Add($"Default flow {f.Id} has condition"); }
        foreach(var ev in events.Where(e=> e.Type=="endEvent"))
        {
            var hasCancel = rawEvDefs?.ContainsKey(ev.Id)==true && rawEvDefs[ev.Id].Any(x=> x.Name.LocalName=="cancelEventDefinition"); if(!hasCancel) hasCancel = ev.Definitions.OfType<CancelEventDefinition>().Any(); if(hasCancel){ string? cursor = ev.SubprocessId; bool insideTx=false; while(cursor!=null){ var sp = subprocesses.FirstOrDefault(s=> s.Id==cursor); if(sp==null) break; if(sp.IsTransaction){ insideTx=true; break;} cursor = sp.SubprocessId; } if(!insideTx) diagnostics.Add($"Cancel end event {ev.Id} outside transaction"); }
        }
        // NEW Phase B: terminate end event outside transaction validation
        foreach (var ev in events.Where(e => e.Type == "endEvent" && e.Definitions.OfType<TerminateEventDefinition>().Any()))
        {
            string? cursor = ev.SubprocessId; bool insideTx = false; while (cursor != null)
            {
                var sp = subprocesses.FirstOrDefault(s => s.Id == cursor); if (sp == null) break; if (sp.IsTransaction) { insideTx = true; break; } cursor = sp.SubprocessId;
            }
            if (!insideTx) diagnostics.Add($"Terminate end event {ev.Id} outside transaction");
        }
        foreach(var gw in gateways) if(!flows.Any(f=> f.SourceRef==gw.Id)) diagnostics.Add($"Gateway {gw.Id} has no outgoing");
        foreach(var (bid, attached) in boundaryEvents){ if(string.IsNullOrEmpty(attached)) continue; if(!flowNodeIds.Contains(attached!)) diagnostics.Add($"boundaryEvent {bid} attachedToRef {attached} missing"); }
        // NEW Phase B: boundary compensation must have cancelActivity='false'
        foreach (var bev in events.Where(e => e.Type == "boundaryEvent" && e.Definitions.OfType<CompensationEventDefinition>().Any()))
        {
            if (elementsMetadata != null && elementsMetadata.TryGetValue(bev.Id, out var meta))
            {
                if (!meta.Attributes.TryGetValue("cancelActivity", out var val) || !string.Equals(val, "false", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add($"Boundary compensation event {bev.Id} must have cancelActivity='false'");
                }
            }
        }
        foreach(var kv in linkThrowCounts){ if(kv.Value>1) diagnostics.Add($"Multiple throw link events for {kv.Key}"); }
        foreach(var kv in linkThrowCounts.Keys){ if(!linkCatchNames.Contains(kv)) diagnostics.Add($"Unmatched link {kv}"); }
        foreach(var f in flows.Where(f=> f.IsDefault)) { var hasCond = !string.IsNullOrWhiteSpace(f.ConditionExpression) || (rawCond!=null && rawCond.TryGetValue(f.Id, out var rc) && !string.IsNullOrEmpty(rc.Raw)); if(hasCond) diagnostics.Add($"Default flow {f.Id} has condition"); }
        if (_options.StrictValidation)
        {
            foreach (var f in flows) if(!flowNodeIds.Contains(f.SourceRef) || !flowNodeIds.Contains(f.TargetRef)) diagnostics.Add($"SequenceFlow {f.Id} has invalid endpoints {f.SourceRef}->{f.TargetRef}");
            if(!events.Any(e=> e.Type=="startEvent")) diagnostics.Add("No startEvent found in process");
        }

        List<BpmnShape>? shapes=null; List<BpmnEdge>? edges=null; if(_options.ParseDiagramInterchange){ var bpmndi=(XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI"; var omgdc=(XNamespace)"http://www.omg.org/spec/DD/20100524/DC"; var omgdi=(XNamespace)"http://www.omg.org/spec/DD/20100524/DI"; shapes=new(); edges=new(); foreach(var shape in doc.Descendants(bpmndi+"BPMNShape")){ var id=Intern(shape.Attribute("id")?.Value ?? string.Empty); var bpmnElement=Intern(shape.Attribute("bpmnElement")?.Value ?? string.Empty); var bounds=shape.Element(omgdc+"Bounds"); if(bounds!=null && double.TryParse(bounds.Attribute("x")?.Value,out var x) && double.TryParse(bounds.Attribute("y")?.Value,out var y) && double.TryParse(bounds.Attribute("width")?.Value,out var w) && double.TryParse(bounds.Attribute("height")?.Value,out var h)) shapes.Add(new BpmnShape(id,bpmnElement,x,y,w,h)); } foreach(var edge in doc.Descendants(bpmndi+"BPMNEdge")){ var id=Intern(edge.Attribute("id")?.Value ?? string.Empty); var bpmnElement=Intern(edge.Attribute("bpmnElement")?.Value ?? string.Empty); var wp=new List<(double X,double Y)>(); foreach(var waypoint in edge.Elements(omgdi+"waypoint")){ if(double.TryParse(waypoint.Attribute("x")?.Value,out var wx) && double.TryParse(waypoint.Attribute("y")?.Value,out var wy)) wp.Add((wx,wy)); } edges.Add(new BpmnEdge(id,bpmnElement,wp)); } if(strict && shapes.Count + edges.Count > 0 && _options.CaptureDiRaw)
                diRoot = doc.Descendants(bpmndi + "BPMNDiagram").FirstOrDefault()?.Parent as XElement; }

        if (subprocesses.Count > 0)
        {
            var updated = new List<BpmnSubprocess>(subprocesses.Count);
            foreach (var sp in subprocesses)
            {
                var childFlowNodes = new List<string>();
                childFlowNodes.AddRange(events.Where(e => e.SubprocessId == sp.Id).Select(e => e.Id));
                childFlowNodes.AddRange(tasks.Where(t => t.SubprocessId == sp.Id).Select(t => t.Id));
                childFlowNodes.AddRange(gateways.Where(g => g.SubprocessId == sp.Id).Select(g => g.Id));
                childFlowNodes.AddRange(subprocesses.Where(s2 => s2.SubprocessId == sp.Id).Select(s2 => s2.Id));
                var childSeqFlows = flows.Where(f => f.SubprocessId == sp.Id).Select(f => f.Id).ToList();
                updated.Add(sp with { ChildFlowNodeIds = childFlowNodes, ChildSequenceFlowIds = childSeqFlows });
            }
            subprocesses = updated;
        }

        var activities= tasks.Cast<object>().Concat(subprocesses);
        BpmnRawMetadata? rawMeta=null; if(strict){ if(_options.OptimizeStrictMemory){ if(rawIncoming is {Count:0}) rawIncoming=null; if(rawOutgoing is {Count:0}) rawOutgoing=null; if(rawCond is {Count:0}) rawCond=null; if(rawExtensions is {Count:0}) rawExtensions=null; if(rawEvDefs is {Count:0}) rawEvDefs=null; if(rawDefinitionsAttr is {Count:0}) rawDefinitionsAttr=null; if(rawProcessAttr is {Count:0}) rawProcessAttr=null; if(rawGlobalElements is {Count:0}) rawGlobalElements=null; if(rawArtifacts is {Count:0}) rawArtifacts=null; if(rawLanes is {Count:0}) rawLanes=null; if(namespacePrefixes is {Count:0}) namespacePrefixes=null; if(elementsMetadata is {Count:0}) elementsMetadata=null; if(rawDocumentation is {Count:0}) rawDocumentation=null; if(rawMultiInstance is {Count:0}) rawMultiInstance=null; if(priorityAttrNs is {Count:0}) priorityAttrNs=null; if(flowNodeAttributes is {Count:0}) flowNodeAttributes=null; }
            rawMeta = new BpmnRawMetadata(rawDefinitionsAttr, rawProcessAttr, rawIncoming?.ToDictionary(k=>k.Key,v=>(IReadOnlyList<string>)v.Value), rawOutgoing?.ToDictionary(k=>k.Key,v=>(IReadOnlyList<string>)v.Value), rawCond?.ToDictionary(k=>k.Key,v=>v.Value), rawExtensions, rawEvDefs?.ToDictionary(k=>k.Key,v=>(IReadOnlyList<XElement>)v.Value), rawMultiInstance?.ToDictionary(k=>k.Key,v=> new XElement(v.Value)), priorityAttrNs?.ToDictionary(k=>k.Key,v=>v.Value), flowNodeAttributes, RoundtripDirty:false, NamespacePrefixes: namespacePrefixes, ElementsMetadata: elementsMetadata, RawGlobalElements: rawGlobalElements, RawArtifacts: rawArtifacts, RawLanes: rawLanes, RawDocumentation: rawDocumentation?.ToDictionary(k=>k.Key,v=>(IReadOnlyList<XElement>)v.Value), RawDiRoot: diRoot); }

        var model = new BpmnModel(pid, events, gateways, subprocesses, flows, tasks, dataObjects, dataObjectRefs, dataStores, dataStoreRefs, properties, activityIo, messageModels, signalModels, errorModels, escalationModels, diagnostics, shapes, edges, participants, lanes, messageFlows, textAnnotations, associationArtifacts, groups, Activities: activities, RawMetadata: rawMeta);
        Cache(xml, model);
        return Task.FromResult(model);
    }

    public string Serialize(BpmnModel model) => new BpmnSerializer { RoundtripMode = _options.RoundtripMode }.Serialize(model);

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
                    list.Add(new TimerEventDefinition(defElem.Element(ns + "timeDate")?.Value ?? defElem.Element("timeDate")?.Value, defElem.Element(ns + "timeDuration")?.Value ?? defElem.Element("timeDuration")?.Value, defElem.Element(ns + "timeCycle")?.Value ?? defElem.Element("timeCycle")?.Value));
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