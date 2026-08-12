//See docs/ROUNDTRIP_STRICT_PLAN.md
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

    public BpmnRoundtripMode RoundtripMode { get; init; } = BpmnRoundtripMode.Normalized;
    /// <summary>If false (strict only), do not synthesize missing incoming/outgoing edges.</summary>
    public bool PreserveGeneratedIfMissing { get; init; } = false;

    public string Serialize(BpmnModel model)
    {
        var strictRequested = RoundtripMode == BpmnRoundtripMode.Strict;
        var strict = strictRequested && model.RawMetadata != null && !model.RawMetadata.RoundtripDirty;
        if (strictRequested && model.RawMetadata?.RoundtripDirty == true)
        {
            if (model.Diagnostics is List<string> dl && !dl.Contains("RT-Fallback:dirty-roundtrip")) dl.Add("RT-Fallback:dirty-roundtrip");
        }
        if (strictRequested && !strict)
        {
            // force normalized path by ignoring raw structures
        }
        var raw = strict ? model.RawMetadata : null;

        // Fallback diagnostics collection (mutate list if underlying collection is mutable)
        if (strict && raw != null)
        {
            var diagList = model.Diagnostics as List<string>;
            if (diagList != null)
            {
                if (raw.RawExtensionElements == null)
                {
                    diagList.Add("RT-Fallback:extensions (RawExtensionElements missing)");
                }
                // Additional hooks for future categories (commented for now)
                // if (raw.RawMultiInstance == null) diagList.Add("RT-Fallback:multiInstance");
                // if (raw.RawEventDefinitions == null) diagList.Add("RT-Fallback:eventDefinitions");
            }
        }

        Dictionary<string, List<string>>? fallbackIncoming = null;
        Dictionary<string, List<string>>? fallbackOutgoing = null;
        if (strict || (strictRequested && PreserveGeneratedIfMissing))
        {
            fallbackIncoming = new();
            fallbackOutgoing = new();
            foreach (var f in model.SequenceFlows)
            {
                if (!fallbackOutgoing.TryGetValue(f.SourceRef, out var lo)) { lo = new(); fallbackOutgoing[f.SourceRef] = lo; }
                lo.Add(f.Id);
                if (!fallbackIncoming.TryGetValue(f.TargetRef, out var li)) { li = new(); fallbackIncoming[f.TargetRef] = li; }
                li.Add(f.Id);
            }
        }

        var vendorNs = new HashSet<string>();
        if (!strict)
        {
            void CollectExt(Dictionary<string,string>? ext)
            {
                if (ext == null) return;
                foreach (var k in ext.Keys)
                {
                    var elemNs = ParseElementNamespace(k);
                    if (elemNs != null && elemNs != Bpmn.NamespaceName) vendorNs.Add(elemNs);
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
            foreach (var sp in model.Subprocesses)
            {
                if (sp.Loop is MultiInstanceLoopCharacteristics mi)
                {
                    if (!string.IsNullOrWhiteSpace(mi.Collection) || !string.IsNullOrWhiteSpace(mi.ElementVariable)) vendorNs.Add("http://camunda.org/schema/1.0/bpmn");
                    if (!string.IsNullOrWhiteSpace(mi.InputElement) || !string.IsNullOrWhiteSpace(mi.OutputElement)) vendorNs.Add("http://zeebe.io/schema/zeebe/1.0");
                }
            }
        }

        // Build definitions root (namespace prefix replay for strict)
        var definitions = new XElement(Bpmn + "definitions");
        List<NamespacePrefix>? originalOrder = null;
        if (strict && raw?.NamespacePrefixes is { Count: >0 })
        {
            originalOrder = raw.NamespacePrefixes.ToList();
            foreach (var np in originalOrder)
            {
                if (string.IsNullOrEmpty(np.Prefix)) definitions.SetAttributeValue("xmlns", np.Uri);
                else definitions.SetAttributeValue(XNamespace.Xmlns + np.Prefix, np.Uri);
            }
            var hasBpmnPrefix = originalOrder.Any(p => p.Uri == Bpmn.NamespaceName && p.Prefix == "bpmn");
            var hasDefaultBpmn = originalOrder.Any(p => p.Uri == Bpmn.NamespaceName && string.IsNullOrEmpty(p.Prefix));
            // Only append synthetic bpmn prefix if neither a bpmn prefix nor a default binding to BPMN exists.
            if (!hasBpmnPrefix && !hasDefaultBpmn)
                definitions.SetAttributeValue(XNamespace.Xmlns + "bpmn", Bpmn.NamespaceName);
        }
        else
        {
            definitions.SetAttributeValue(XNamespace.Xmlns + "bpmn", Bpmn.NamespaceName);
        }

        // collect new prefixes introduced only inside raw extension elements (strict)
        if (strict && raw?.RawExtensionElements != null)
        {
            var known = new HashSet<string>(definitions.Attributes().Where(a => a.IsNamespaceDeclaration).Select(a => a.Value));
            foreach (var kv in raw.RawExtensionElements)
            {
                foreach (var attr in kv.Value.Attributes())
                {
                    if (!attr.IsNamespaceDeclaration) continue;
                    var uri = attr.Value;
                    if (known.Add(uri))
                    {
                        var prefix = attr.Name.LocalName == "xmlns" ? string.Empty : attr.Name.LocalName;
                        if (string.IsNullOrEmpty(prefix))
                        {
                            // avoid overriding default namespace, assign synthetic
                            prefix = "ns_ext" + known.Count;
                        }
                        definitions.SetAttributeValue(XNamespace.Xmlns + prefix, uri);
                    }
                }
            }
        }

        if (strict && raw?.DefinitionsAttributes != null)
        {
            foreach (var kv in raw.DefinitionsAttributes)
            {
                var local = ParseLocalName(kv.Key);
                var nsUri = ParseNamespace(kv.Key);
                if (nsUri == null)
                {
                    if (definitions.Attribute(local) == null) definitions.Add(new XAttribute(local, kv.Value));
                }
                else
                {
                    var xname = XName.Get(local, nsUri);
                    if (definitions.Attribute(xname) == null) definitions.Add(new XAttribute(xname, kv.Value));
                }
            }
        }
        else if (!strict)
        {
            foreach (var uri in vendorNs)
            {
                if (!WellKnownPrefixes.TryGetValue(uri, out var prefix)) prefix = uri.Contains('/') ? uri.TrimEnd('/').Split('/').Last().Replace('.', '_').Replace('-', '_') : "ns";
                definitions.SetAttributeValue(XNamespace.Xmlns + prefix, uri);
            }
        }

        // Strict: output raw global elements before process to preserve availability (ordering fidelity enhancement could be added later)
        if (strict && raw?.RawGlobalElements is { Count: > 0 })
        {
            foreach (var ge in raw.RawGlobalElements) definitions.Add(new XElement(ge));
        }
        else if (strict)
        {
            // Strict fallback: raw global elements not captured, emit minimal set from model if available
            if (model.Messages is { Count: >0 })
            {
                foreach (var m in model.Messages)
                {
                    if (string.IsNullOrEmpty(m.Id)) continue;
                    var msgEl = new XElement(Bpmn + "message", new XAttribute("id", m.Id));
                    if (!string.IsNullOrWhiteSpace(m.Name)) msgEl.SetAttributeValue("name", m.Name);
                    definitions.Add(msgEl);
                }
            }
            if (model.Signals is { Count: >0 })
            {
                foreach (var s in model.Signals)
                {
                    if (string.IsNullOrEmpty(s.Id)) continue;
                    var sigEl = new XElement(Bpmn + "signal", new XAttribute("id", s.Id));
                    if (!string.IsNullOrWhiteSpace(s.Name)) sigEl.SetAttributeValue("name", s.Name);
                    definitions.Add(sigEl);
                }
            }
        }
        else if (!strict)
        {
            // Fallback / normalized: emit minimal global elements from model (lossy attributes already normalized)
            if (model.Messages is { Count: >0 })
            {
                foreach (var m in model.Messages)
                {
                    if (string.IsNullOrEmpty(m.Id)) continue;
                    var msgEl = new XElement(Bpmn + "message", new XAttribute("id", m.Id));
                    if (!string.IsNullOrWhiteSpace(m.Name)) msgEl.SetAttributeValue("name", m.Name);
                    definitions.Add(msgEl);
                }
            }
            if (model.Signals is { Count: >0 })
            {
                foreach (var s in model.Signals)
                {
                    if (string.IsNullOrEmpty(s.Id)) continue;
                    var sigEl = new XElement(Bpmn + "signal", new XAttribute("id", s.Id));
                    if (!string.IsNullOrWhiteSpace(s.Name)) sigEl.SetAttributeValue("name", s.Name);
                    definitions.Add(sigEl);
                }
            }
            if (model.Errors is { Count: >0 })
            {
                foreach (var e in model.Errors)
                {
                    if (string.IsNullOrEmpty(e.Id)) continue;
                    var errEl = new XElement(Bpmn + "error", new XAttribute("id", e.Id));
                    if (!string.IsNullOrWhiteSpace(e.Name)) errEl.SetAttributeValue("name", e.Name);
                    if (!string.IsNullOrWhiteSpace(e.ErrorCode)) errEl.SetAttributeValue("errorCode", e.ErrorCode);
                    definitions.Add(errEl);
                }
            }
            if (model.Escalations is { Count: >0 })
            {
                foreach (var esc in model.Escalations)
                {
                    if (string.IsNullOrEmpty(esc.Id)) continue;
                    var escEl = new XElement(Bpmn + "escalation", new XAttribute("id", esc.Id));
                    if (!string.IsNullOrWhiteSpace(esc.Name)) escEl.SetAttributeValue("name", esc.Name);
                    if (!string.IsNullOrWhiteSpace(esc.EscalationCode)) escEl.SetAttributeValue("escalationCode", esc.EscalationCode);
                    definitions.Add(escEl);
                }
            }
        }

        var proc = new XElement(Bpmn + "process", new XAttribute("id", model.ProcessId));
        if (strict && raw?.ProcessAttributes != null)
        {
            foreach (var kv in raw.ProcessAttributes)
            {
                var local = ParseLocalName(kv.Key);
                if (local == "id") continue;
                var nsUri = ParseNamespace(kv.Key);
                if (nsUri == null)
                {
                    if (proc.Attribute(local) == null) proc.Add(new XAttribute(local, kv.Value));
                }
                else
                {
                    var xname = XName.Get(local, nsUri);
                    if (proc.Attribute(xname) == null) proc.Add(new XAttribute(xname, kv.Value));
                }
            }
            if (raw.RawDocumentation != null && raw.RawDocumentation.TryGetValue("__process", out var pdocs)) foreach (var d in pdocs) proc.Add(new XElement(d));
        }
        definitions.Add(proc);

        // Helper: add raw documentation for an element id (strict roundtrip)
        void AddRawDocumentation(string id, XElement node)
        {
            if (!strict) return;
            if (raw?.RawDocumentation != null && raw.RawDocumentation.TryGetValue(id, out var docs)) foreach (var d in docs) node.Add(new XElement(d));
        }

        // Helpers
        void AddExtensionsNormalized(XElement parent, Dictionary<string,string>? ext)
        {
            if (ext == null || ext.Count == 0) return;
            var extRoot = new XElement(Bpmn + "extensionElements");
            var elementMap = new Dictionary<string,XElement>();
            foreach (var kv in ext)
            {
                if (!TryParseExtensionKey(kv.Key, out var nsUri, out var localName, out var attrName, out var attrNsUri)) continue;
                var key = nsUri + "|" + localName;
                if (!elementMap.TryGetValue(key, out var el))
                {
                    XNamespace nsx = nsUri;
                    el = new XElement(nsx + localName);
                    elementMap[key] = el;
                    extRoot.Add(el);
                }
                if (attrName == "__present") continue;
                el.SetAttributeValue(string.IsNullOrEmpty(attrNsUri) ? XName.Get(attrName) : XName.Get(attrName, attrNsUri), kv.Value);
            }
            if (elementMap.Count > 0) parent.Add(extRoot);
        }

        XElement? AttachRawExtensions(string id, XElement target)
        {
            if (!strict || raw?.RawExtensionElements == null) return target;
            if (raw.RawExtensionElements.TryGetValue(id, out var rawExt))
            {
                // Deep clone immutable snapshot: work on a copy of the stored extensionElements to avoid external mutation influencing output
                target.Add(new XElement(rawExt));
            }
            return target;
        }

        void AppendInOutIfStrict(string id, XElement node)
        {
            if (!strict) return;
            IReadOnlyList<string>? incList = null; IReadOnlyList<string>? outList = null;
            if (raw?.Incoming != null && raw.Incoming.TryGetValue(id, out var ri)) incList = ri;
            else if (PreserveGeneratedIfMissing && fallbackIncoming != null && fallbackIncoming.TryGetValue(id, out var fi)) incList = fi;
            if (raw?.Outgoing != null && raw.Outgoing.TryGetValue(id, out var ro)) outList = ro;
            else if (PreserveGeneratedIfMissing && fallbackOutgoing != null && fallbackOutgoing.TryGetValue(id, out var fo)) outList = fo;
            if (incList != null && incList.Count > 0) foreach (var iid in incList) node.Add(new XElement(Bpmn + "incoming", iid));
            if (outList != null && outList.Count > 0) foreach (var oid in outList) node.Add(new XElement(Bpmn + "outgoing", oid));
        }

        void ApplyOriginalAttributes(string id, XElement el)
        {
            if (!strict || raw?.FlowNodeAttributes == null) return;
            var isPartialDirty = raw.PartiallyDirtyElements != null && raw.PartiallyDirtyElements.Contains(id);
            if (!raw.FlowNodeAttributes.TryGetValue(id, out var attrs)) return;
            foreach (var kv in attrs)
            {
                var local = ParseLocalName(kv.Key);
                if (local == "id") continue;
                if (local == "name" && isPartialDirty) continue; // skip original to allow mutated attribute injection later
                if (local == "name" && string.IsNullOrEmpty(kv.Value)) continue;
                if (string.IsNullOrEmpty(local) || local.IndexOf('/') >= 0 || local.Any(ch => char.IsWhiteSpace(ch))) continue;
                var nsUri = ParseNamespace(kv.Key);
                XName xname = nsUri == null ? local : XName.Get(local, nsUri);
                if (el.Attribute(xname) == null) el.Add(new XAttribute(xname, kv.Value));
            }
            // inject mutated name for partial dirty (task lookup only)
            if (isPartialDirty)
            {
                var task = model.Tasks.FirstOrDefault(t => t.Id == id);
                if (task != null && !string.IsNullOrEmpty(task.Name)) el.SetAttributeValue("name", task.Name);
            }
        }

        // order list
        var orderedElements = strict && raw?.ElementsMetadata != null ? raw.ElementsMetadata.OrderBy(k => k.Value.OrderIndex).Select(k => k.Key).ToList() : null;
        var elementLookup = new Dictionary<string,XElement>();

        XElement BuildSubProcess(BpmnSubprocess sp)
        {
            var spEl = new XElement(Bpmn + "subProcess", new XAttribute("id", sp.Id));
            if (!strict && sp.Loop is MultiInstanceLoopCharacteristics mi)
            {
                var miEl = new XElement(Bpmn + "multiInstanceLoopCharacteristics");
                if (mi.IsSequential) miEl.Add(new XAttribute("isSequential", "true"));
                if (mi.LoopCardinality.HasValue) miEl.Add(new XElement(Bpmn + "loopCardinality", mi.LoopCardinality.Value));
                if (!string.IsNullOrWhiteSpace(mi.Collection)) miEl.Add(new XAttribute(XName.Get("collection", "http://camunda.org/schema/1.0/bpmn"), mi.Collection));
                if (!string.IsNullOrWhiteSpace(mi.ElementVariable)) miEl.Add(new XAttribute(XName.Get("elementVariable", "http://camunda.org/schema/1.0/bpmn"), mi.ElementVariable));
                if (!string.IsNullOrWhiteSpace(mi.InputElement)) miEl.Add(new XElement(XName.Get("inputElement", "http://zeebe.io/schema/zeebe/1.0"), mi.InputElement));
                if (!string.IsNullOrWhiteSpace(mi.OutputElement)) miEl.Add(new XElement(XName.Get("outputElement", "http://zeebe.io/schema/zeebe/1.0"), mi.OutputElement));
                if (!string.IsNullOrWhiteSpace(mi.CompletionCondition)) miEl.Add(new XElement(Bpmn + "completionCondition", mi.CompletionCondition));
                spEl.Add(miEl);
            }
            else if (strict && raw?.RawMultiInstance != null && raw.RawMultiInstance.TryGetValue(sp.Id, out var rawMi)) spEl.Add(new XElement(rawMi));
            if (strict) AttachRawExtensions(sp.Id, spEl); else AddExtensionsNormalized(spEl, sp.ExtensionAttributes);
            ApplyOriginalAttributes(sp.Id, spEl); AddRawDocumentation(sp.Id, spEl); AppendInOutIfStrict(sp.Id, spEl); return spEl;
        }

        void Register(string id, XElement el) { if (orderedElements != null && !string.IsNullOrEmpty(id)) elementLookup[id] = el; else proc.Add(el); }

        foreach (var sp in model.Subprocesses) Register(sp.Id, BuildSubProcess(sp));
        foreach (var evt in model.Events)
        { var evtEl = new XElement(Bpmn + evt.Type, new XAttribute("id", evt.Id)); if (strict && raw?.RawEventDefinitions != null && raw.RawEventDefinitions.TryGetValue(evt.Id, out var rawDefs)) foreach (var rd in rawDefs) evtEl.Add(new XElement(rd)); else foreach (var def in evt.Definitions) evtEl.Add(SerializeEventDefinition(def)); if (strict) AttachRawExtensions(evt.Id, evtEl); else AddExtensionsNormalized(evtEl, evt.ExtensionAttributes); ApplyOriginalAttributes(evt.Id, evtEl); AddRawDocumentation(evt.Id, evtEl); AppendInOutIfStrict(evt.Id, evtEl); Register(evt.Id, evtEl); }
        foreach (var gw in model.Gateways)
        { var gwEl = new XElement(Bpmn + gw.Type, new XAttribute("id", gw.Id)); if (!string.IsNullOrWhiteSpace(gw.DefaultFlowId)) gwEl.Add(new XAttribute("default", gw.DefaultFlowId)); if (strict) AttachRawExtensions(gw.Id, gwEl); else AddExtensionsNormalized(gwEl, gw.ExtensionAttributes); ApplyOriginalAttributes(gw.Id, gwEl); AddRawDocumentation(gw.Id, gwEl); AppendInOutIfStrict(gw.Id, gwEl); Register(gw.Id, gwEl); }
        foreach (var task in model.Tasks)
        { var taskEl = new XElement(Bpmn + task.Type, new XAttribute("id", task.Id)); if (!strict && !string.IsNullOrWhiteSpace(task.Name)) taskEl.SetAttributeValue("name", task.Name); if (strict && raw?.RawMultiInstance != null && raw.RawMultiInstance.TryGetValue(task.Id, out var rawMi)) taskEl.Add(new XElement(rawMi)); if (strict) AttachRawExtensions(task.Id, taskEl); else AddExtensionsNormalized(taskEl, task.Attributes); ApplyOriginalAttributes(task.Id, taskEl); AddRawDocumentation(task.Id, taskEl); AppendInOutIfStrict(task.Id, taskEl); Register(task.Id, taskEl); }
        foreach (var f in model.SequenceFlows)
        { var fEl = new XElement(Bpmn + "sequenceFlow", new XAttribute("id", f.Id), new XAttribute("sourceRef", f.SourceRef), new XAttribute("targetRef", f.TargetRef)); if (f.Priority.HasValue){ if (strict && raw?.PriorityAttributeNamespace != null && raw.PriorityAttributeNamespace.TryGetValue(f.Id, out var pns)){ if (string.IsNullOrEmpty(pns)) fEl.SetAttributeValue("priority", f.Priority.Value); else fEl.SetAttributeValue(XName.Get("priority", pns), f.Priority.Value);} else fEl.SetAttributeValue(XName.Get("priority", "http://vertexbpmn.io/schema/1.0"), f.Priority.Value);} if (strict && raw?.SequenceFlowConditions != null && raw.SequenceFlowConditions.TryGetValue(f.Id, out var rcond)){ if (!string.IsNullOrEmpty(rcond.Raw)){ var condEl = new XElement(Bpmn + "conditionExpression"); if (rcond.WasCData) condEl.Add(new XCData(rcond.Raw)); else condEl.Value = rcond.Raw; fEl.Add(condEl);} } else if (!string.IsNullOrWhiteSpace(f.ConditionExpression)) fEl.Add(new XElement(Bpmn + "conditionExpression", new XCData(f.ConditionExpression))); if (strict) AttachRawExtensions(f.Id, fEl); else AddExtensionsNormalized(fEl, f.ExtensionAttributes); ApplyOriginalAttributes(f.Id, fEl); AddRawDocumentation(f.Id, fEl); Register(f.Id, fEl); }
        if (orderedElements != null){ foreach (var id in orderedElements){ if (elementLookup.TryGetValue(id, out var el)) proc.Add(el); } }
        // Strict re-emit artifacts (textAnnotation, group, association) & lanes if captured
        if (strict && raw?.RawArtifacts is { Count: >0 }) foreach (var art in raw.RawArtifacts) proc.Add(new XElement(art));
        if (strict && raw?.RawLanes is { Count: >0 })
        {
            // RawLanes currently contains laneSet elements (with nested lanes) PLUS individual lane elements captured during walk.
            // We only need to add the laneSet elements to preserve original hierarchy. Standalone lane duplicates would break structure.
            var laneSets = raw.RawLanes.Where(x => x.Name.LocalName == "laneSet").ToList();
            if (laneSets.Count > 0)
            {
                foreach (var ls in laneSets)
                {
                    proc.Add(new XElement(ls)); // deep clone preserves internal lane + flowNodeRef order
                }
                // Skip adding individual lanes that belong to any emitted laneSet (they are already inside).
                // If there are lane elements without a parent laneSet (edge case), add those separately.
                var laneIdsInSets = new HashSet<string>(laneSets.SelectMany(ls => ls.Elements().Where(e => e.Name.LocalName == "lane").Select(e => (string?)e.Attribute("id")).Where(id => !string.IsNullOrEmpty(id))!).OfType<string>());
                foreach (var ln in raw.RawLanes.Where(x => x.Name.LocalName == "lane"))
                {
                    var id = (string?)ln.Attribute("id");
                    if (!string.IsNullOrEmpty(id) && laneIdsInSets.Contains(id)) continue; // already present in laneSet
                    proc.Add(new XElement(ln));
                }
            }
            else
            {
                // Fallback: no laneSet captured, emit lanes as previously
                foreach (var ln in raw.RawLanes) proc.Add(new XElement(ln));
            }
        }

        // Post-pass: ensure generated incoming/outgoing if requested and still absent
        if (strict && PreserveGeneratedIfMissing && fallbackIncoming != null && fallbackOutgoing != null)
        {
            // Build quick lookup for process child elements by id
            var nodeById = proc.Elements().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id")!, e => e);
            foreach (var kv in fallbackIncoming)
            {
                if (!nodeById.TryGetValue(kv.Key, out var el)) continue;
                // if element already has incoming we skip
                if (!el.Elements(Bpmn + "incoming").Any())
                {
                    foreach (var fid in kv.Value) el.Add(new XElement(Bpmn + "incoming", fid));
                }
            }
            foreach (var kv in fallbackOutgoing)
            {
                if (!nodeById.TryGetValue(kv.Key, out var el)) continue;
                if (!el.Elements(Bpmn + "outgoing").Any())
                {
                    foreach (var fid in kv.Value) el.Add(new XElement(Bpmn + "outgoing", fid));
                }
            }
        }

        if (!strict)
        {
            foreach (var d in model.DataObjects) proc.Add(new XElement(Bpmn + "dataObject", new XAttribute("id", d.Id), d.Name != null ? new XAttribute("name", d.Name) : null));
            foreach (var dr in model.DataObjectReferences) proc.Add(new XElement(Bpmn + "dataObjectReference", new XAttribute("id", dr.Id), new XAttribute("dataObjectRef", dr.DataObjectRef)));
            foreach (var ds in model.DataStores) proc.Add(new XElement(Bpmn + "dataStore", new XAttribute("id", ds.Id), ds.Name != null ? new XAttribute("name", ds.Name) : null));
            foreach (var dsr in model.DataStoreReferences) proc.Add(new XElement(Bpmn + "dataStoreReference", new XAttribute("id", dsr.Id), new XAttribute("dataStoreRef", dsr.DataStoreRef)));
            foreach (var p in model.Properties) proc.Add(new XElement(Bpmn + "property", new XAttribute("id", p.Id), p.Name != null ? new XAttribute("name", p.Name) : null));
        }

        if (!strict)
        {
            foreach (var io in model.ActivityIo)
            {
                var act = proc.Elements().FirstOrDefault(e => (string?)e.Attribute("id") == io.ActivityId);
                if (act == null) continue;
                var ioSpec = new XElement(Bpmn + "ioSpecification");
                foreach (var i in io.DataInputs) ioSpec.Add(new XElement(Bpmn + "dataInput", new XAttribute("id", i.Id), i.Name != null ? new XAttribute("name", i.Name) : null));
                foreach (var o in io.DataOutputs) ioSpec.Add(new XElement(Bpmn + "dataOutput", new XAttribute("id", o.Id), o.Name != null ? new XAttribute("name", o.Name) : null));
                act.Add(ioSpec);
                foreach (var ina in io.InputAssociations) act.Add(new XElement(Bpmn + "dataInputAssociation", new XElement(Bpmn + "sourceRef", ina.SourceRef), new XElement(Bpmn + "targetRef", ina.TargetRef)));
                foreach (var oa in io.OutputAssociations) act.Add(new XElement(Bpmn + "dataOutputAssociation", new XElement(Bpmn + "sourceRef", oa.SourceRef), new XElement(Bpmn + "targetRef", oa.TargetRef)));
            }
        }

        if (!strict && ((model.Shapes?.Count > 0) || (model.Edges?.Count > 0)))
        {
            var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
            var omgdc = (XNamespace)"http://www.omg.org/spec/DD/20100524/DC";
            var omgdi = (XNamespace)"http://www.omg.org/spec/DD/20100524/DI";
            if (definitions.Attribute(XNamespace.Xmlns + "bpmndi") == null) definitions.SetAttributeValue(XNamespace.Xmlns + "bpmndi", bpmndi.NamespaceName);
            if (definitions.Attribute(XNamespace.Xmlns + "omgdc") == null) definitions.SetAttributeValue(XNamespace.Xmlns + "omgdc", omgdc.NamespaceName);
            if (definitions.Attribute(XNamespace.Xmlns + "omgdi") == null) definitions.SetAttributeValue(XNamespace.Xmlns + "omgdi", omgdi.NamespaceName);
            var diagram = new XElement(bpmndi + "BPMNDiagram", new XAttribute("id", $"{model.ProcessId}_diagram"));
            var plane = new XElement(bpmndi + "BPMNPlane", new XAttribute("bpmnElement", model.ProcessId));
            diagram.Add(plane);
            if (model.Shapes != null) foreach (var s in model.Shapes) plane.Add(new XElement(bpmndi + "BPMNShape", new XAttribute("id", s.Id), new XAttribute("bpmnElement", s.BpmnElementId), new XElement(omgdc + "Bounds", new XAttribute("x", s.X), new XAttribute("y", s.Y), new XAttribute("width", s.Width), new XAttribute("height", s.Height))));
            if (model.Edges != null) foreach (var e in model.Edges) plane.Add(new XElement(bpmndi + "BPMNEdge", new XAttribute("id", e.Id), new XAttribute("bpmnElement", e.BpmnElementId), e.Waypoints.Select(wp => new XElement(omgdi + "waypoint", new XAttribute("x", wp.X), new XAttribute("y", wp.Y)))));
            definitions.Add(diagram);
        }
        else if (strict && raw?.RawDiRoot != null)
        {
            // Strict Raw DI replay: extract original BPMNDiagram elements
            var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
            // ensure namespace declarations if missing
            if (definitions.Attribute(XNamespace.Xmlns + "bpmndi") == null)
                definitions.SetAttributeValue(XNamespace.Xmlns + "bpmndi", bpmndi.NamespaceName);
            foreach (var diag in raw.RawDiRoot.Elements(bpmndi + "BPMNDiagram"))
            {
                definitions.Add(new XElement(diag));
            }
        }

        var doc = new XDocument(definitions);

        // Final safeguard: if strict (or strict requested fallback) and generation requested ensure incoming/outgoing present
        if ((strict || (strictRequested && PreserveGeneratedIfMissing)) && PreserveGeneratedIfMissing)
        {
            var procEl = definitions.Elements(Bpmn + "process").FirstOrDefault(e => (string?)e.Attribute("id") == model.ProcessId);
            if (procEl != null)
            {
                var nodeById = procEl.Elements().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id")!, e => e);
                foreach (var flow in model.SequenceFlows)
                {
                    if (nodeById.TryGetValue(flow.SourceRef, out var src))
                    {
                        bool hasOut = src.Elements(Bpmn + "outgoing").Any(x => (string)x == flow.Id);
                        if (!hasOut) src.Add(new XElement(Bpmn + "outgoing", flow.Id));
                    }
                    if (nodeById.TryGetValue(flow.TargetRef, out var tgt))
                    {
                        bool hasIn = tgt.Elements(Bpmn + "incoming").Any(x => (string)x == flow.Id);
                        if (!hasIn) tgt.Add(new XElement(Bpmn + "incoming", flow.Id));
                    }
                }
            }
        }
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement SerializeEventDefinitionsPlaceholder(BpmnEvent evt) => new("__defs");
    private static string ParseLocalName(string qname) => qname.LastIndexOf(':') is var idx and >= 0 ? qname[(idx + 1)..] : qname.StartsWith("{") ? qname[(qname.IndexOf('}') + 1)..] : qname;
    private static string? ParseNamespace(string qname) { if (!qname.StartsWith("{")) return null; var close = qname.IndexOf('}'); return close > 1 ? qname[1..close] : null; }
    private static bool TryParseExtensionKey(string key, out string nsUri, out string localName, out string attrName, out string attrNsUri)
    {
        nsUri = localName = attrName = attrNsUri = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;

        // Engine keys use "elementQName.attributeQName". Older snapshots use
        // "{namespace}element:attribute". Do not split every colon: URIs and
        // qualified names both legitimately contain colons.
        string elementQName;
        var closeBrace = key.IndexOf('}');
        var dot = key.IndexOf('.', closeBrace >= 0 ? closeBrace + 1 : 0);
        if (dot > 0)
        {
            elementQName = key[..dot];
            var attributeQName = key[(dot + 1)..];
            if (!TryParseQualifiedName(elementQName, out nsUri, out localName)) return false;
            if (attributeQName == "__present") { attrName = attributeQName; return true; }
            return TryParseQualifiedName(attributeQName, out attrNsUri, out attrName);
        }

        if (closeBrace > 0)
        {
            var separator = key.IndexOf(':', closeBrace + 1);
            if (separator <= closeBrace + 1) return false;
            elementQName = key[..separator];
            attrName = key[(separator + 1)..];
            if (!TryParseQualifiedName(elementQName, out nsUri, out localName)) return false;
            return IsValidXmlLocalName(attrName);
        }

        // A presence-only legacy key has no attribute separator.
        if (!TryParseQualifiedName(key, out nsUri, out localName)) return false;
        attrName = "__present";
        return true;
    }

    private static bool TryParseQualifiedName(string qname, out string nsUri, out string localName)
    {
        nsUri = localName = string.Empty;
        if (qname.StartsWith("{"))
        {
            var close = qname.IndexOf('}');
            if (close <= 1) return false;
            nsUri = qname[1..close];
            localName = qname[(close + 1)..];
        }
        else
        {
            var colon = qname.IndexOf(':');
            if (colon >= 0)
            {
                if (colon == 0 || colon == qname.Length - 1) return false;
                var prefix = qname[..colon];
                localName = qname[(colon + 1)..];
                nsUri = NamespaceByPrefix.TryGetValue(prefix, out var mapped) ? mapped : string.Empty;
            }
            else localName = qname;
        }
        return IsValidXmlLocalName(localName);
    }

    private static bool IsValidXmlLocalName(string name)
    {
        try { System.Xml.XmlConvert.VerifyNCName(name); return true; }
        catch (System.Xml.XmlException) { return false; }
    }

    private static readonly Dictionary<string, string> NamespaceByPrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["camunda"] = "http://camunda.org/schema/1.0/bpmn",
        ["zeebe"] = "http://zeebe.io/schema/zeebe/1.0",
        ["vertex"] = "http://vertexbpmn.io/schema/1.0",
        ["xsi"] = "http://www.w3.org/2001/XMLSchema-instance",
        ["w4graph"] = "http://www.w4.eu/spec/BPMN/20110930/GRAPH"
    };

    private static string? ParseElementNamespace(string key)
    {
        if (key.StartsWith('{'))
        {
            var brace = key.IndexOf('}');
            if (brace > 1) return key.Substring(1, brace - 1);
        }
        var separator = key.IndexOfAny([':', '.']);
        if (separator > 0 && NamespaceByPrefix.TryGetValue(key[..separator], out var uri)) return uri;
        return null;
    }
    private static XElement SerializeEventDefinition(EventDefinition def) => def switch
    { TimerEventDefinition t => new(Bpmn + "timerEventDefinition", t.TimeDate != null ? new XElement(Bpmn + "timeDate", t.TimeDate) : null, t.TimeDuration != null ? new XElement(Bpmn + "timeDuration", t.TimeDuration) : null, t.TimeCycle != null ? new XElement(Bpmn + "timeCycle", t.TimeCycle) : null),
      MessageEventDefinition m => new(Bpmn + "messageEventDefinition", new XAttribute("messageRef", m.MessageRef)),
      SignalEventDefinition s => new(Bpmn + "signalEventDefinition", new XAttribute("signalRef", s.SignalRef)),
      ErrorEventDefinition e => new(Bpmn + "errorEventDefinition", new XAttribute("errorRef", e.ErrorRef)),
      ConditionalEventDefinition c => new(Bpmn + "conditionalEventDefinition", new XElement(Bpmn + "conditionExpression", new XCData(c.Condition ?? string.Empty))),
      TerminateEventDefinition => new(Bpmn + "terminateEventDefinition"),
      CancelEventDefinition => new(Bpmn + "cancelEventDefinition"),
      CompensationEventDefinition cmp => new(Bpmn + "compensateEventDefinition", cmp.ActivityRef != null ? new XAttribute("activityRef", cmp.ActivityRef) : null),
      EscalationEventDefinition esc => new(Bpmn + "escalationEventDefinition", new XAttribute("escalationRef", esc.EscalationRef)),
      LinkEventDefinition link => new(Bpmn + "linkEventDefinition", new XAttribute("name", link.Name)),
      _ => new(Bpmn + "unsupportedEventDefinition") };
}
