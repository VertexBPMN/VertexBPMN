
using Microsoft.Diagnostics.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Extensions;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;
using Xunit.Internal;
using Definitions = VertexBPMN.Domain.Model.Bpmn.Definitions;
using Group = VertexBPMN.Domain.Model.Bpmn.Group;

namespace VertexBPMN.Domain.Model;


public class BpmnParser
{
    private readonly ILogger<BpmnParser> _logger;

    // Raw reference capture for forward resolution (additive, zero-break)
    private static readonly Dictionary<string, (string? SourceRef, string? TargetRef)> _rawSequenceFlowRefs = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string?> _rawBoundaryAttachRefs = new(StringComparer.Ordinal);

    public BpmnParser() : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<BpmnParser>.Instance) { }
    public BpmnParser(ILogger<BpmnParser> logger)
    {
        _logger = logger;
    }

    private static readonly Lazy<XmlSchemaSet> _bpmnSchemas = new(() =>
    {
        var set = new XmlSchemaSet();
        // Add required BPMN 2.0 schemas (ensure these .xsd files are included in your project or resolved from a known path)
        // Example file names; adjust paths as needed:
        // MODEL.xsd, DI.xsd, DC.xsd, BPMNDI.xsd
        set.XmlResolver = null; // Prevent external resolution
        set.Add("http://www.omg.org/spec/BPMN/20100524/MODEL", "Schemas/BPMN20/BPMN20.xsd");
        set.Add("http://www.omg.org/spec/BPMN/20100524/DI", "Schemas/BPMN20/BPMNDI.xsd");
        set.Add("http://www.omg.org/spec/DD/20100524/DC", "Schemas/BPMN20/DC.xsd");
        set.Add("http://www.omg.org/spec/DD/20100524/DI", "Schemas/BPMN20/DI.xsd");
        set.Add("http://www.omg.org/spec/BPMN/20100524/MODEL", "Schemas/BPMN20/Semantic.xsd");
        set.Compile();
        return set;
    });

    public async Task<BpmnModel> ParseAsync(string bpmnXml)
    {
        var diagnostics = new List<string>();
        try
        {
            // 1. Early prefix sanity (helps if XDocument.Parse später scheitert)
            var unboundPrefixes = FindUnboundPrefixes(bpmnXml);
            if (unboundPrefixes.Count > 0)
            {
                diagnostics.AddRange(unboundPrefixes.Select(p => $"Error: Unbound XML namespace prefix '{p}'. Add xmlns:{p}=\"...\" to <definitions>."));
                throw new BpmnSchemaValidationException(diagnostics);
            }

            ValidateXml(bpmnXml, diagnostics);
            if (diagnostics.Any(d => d.StartsWith("Error:", StringComparison.Ordinal)))
                throw new BpmnSchemaValidationException(diagnostics);

            var model = BpmnSerializer.Deserialize(bpmnXml);
            return model;           
        }
        catch (XmlSchemaValidationException ex)
        {
            _logger.LogError(ex, "Schema validation failed");
            throw new BpmnParseException("Schema validation failed", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during BPMN parse");
            throw new BpmnParseException("Unexpected error during BPMN parse", ex);
        }
    }

    private static List<string> FindUnboundPrefixes(string xml)
    {
        if (string.IsNullOrEmpty(xml))
            return new List<string>();

        // Collect every prefix used in element or attribute names of the form prefix:LocalName
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(
                     xml, @"\s([A-Za-z_][\w\-\.]*):[A-Za-z_][\w\-\.]*="))
        {
            used.Add(m.Groups[1].Value);
        }

        foreach (Match m in Regex.Matches(
                     xml, @"\s([A-Za-z_][\w\-\.]*):[A-Za-z_][\w\-\.]*="))
        {
            used.Add(m.Groups[1].Value);
        }

        // Collect declared prefixes (xmlns:prefix="...")
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(
                     xml, @"xmlns:([A-Za-z_][\w\-\.]*)\s*="))
        {
            declared.Add(m.Groups[1].Value);
        }

        // Standard BPMN & DI prefixes may be absent if the file uses default namespace; treat them as implicitly declared.
        // (No vendor allow-list here anymore.)
        string[] implicitStandard =
        {
            "bpmn","bpmndi","di","dc","omgdi","omgdc","xsi","xmlns"
        };
        foreach (var std in implicitStandard)
            declared.Add(std);

        // Return only prefixes used but not declared.
        return used.Where(p => !declared.Contains(p)).ToList();
    }

    private static string ExtractErrorContext(string xml, int line, int pos, int radius = 80)
    {
        if (line <= 0 || pos <= 0) return "<no context>";
        using var sr = new StringReader(xml);
        string? current;
        int ln = 1;
        while ((current = sr.ReadLine()) != null)
        {
            if (ln == line)
            {
                var start = Math.Max(0, pos - 1 - radius / 2);
                var len = Math.Min(radius, Math.Max(0, current.Length - start));
                var snippet = current.Substring(start, len);
                return snippet;
            }
            ln++;
        }
        return "<line out of range>";
    }
    private void ValidateXml(string xml, List<string> diagnostics)
    {
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        if (diagnostics is null) throw new ArgumentNullException(nameof(diagnostics));

        if (string.IsNullOrWhiteSpace(xml))
        {
            diagnostics.Add("Error: Input BPMN XML is empty.");
            return;
        }


        // Defensive size guard (tunable – avoids pathological payloads)
        const long MaxChars = 25_000_000; // ~25 MB of character data
        if (xml.Length > MaxChars)
        {
            diagnostics.Add($"Error: BPMN XML exceeds maximum allowed size ({MaxChars} chars).");
            return;
        }
        var result = BpmnSerializer.ValidateXml(xml);

        if (result is not { IsValid: true })
        {
            result.Errors.ForEach(diagnostics.Add);
        }
    }
    private void ValidateXmlAgainstSchemas(string xml, List<string> diagnostics)
    {
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        if (diagnostics is null) throw new ArgumentNullException(nameof(diagnostics));

        if (string.IsNullOrWhiteSpace(xml))
        {
            diagnostics.Add("Error: Input BPMN XML is empty.");
            return;
        }

        // Defensive size guard (tunable – avoids pathological payloads)
        const long MaxChars = 25_000_000; // ~25 MB of character data
        if (xml.Length > MaxChars)
        {
            diagnostics.Add($"Error: BPMN XML exceeds maximum allowed size ({MaxChars} chars).");
            return;
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = _bpmnSchemas.Value,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            Async = false,
            IgnoreWhitespace = false,
            IgnoreComments = false,
            MaxCharactersInDocument = MaxChars
        };

        settings.ValidationFlags =
            XmlSchemaValidationFlags.ReportValidationWarnings |
            XmlSchemaValidationFlags.ProcessIdentityConstraints |
            XmlSchemaValidationFlags.ProcessInlineSchema |
            XmlSchemaValidationFlags.ProcessSchemaLocation;

        var hadSchemaError = false;

        settings.ValidationEventHandler += (_, args) =>
        {
            var ex = args.Exception;
            var loc = ex is null ? "" : $" (line {ex.LineNumber}, pos {ex.LinePosition})";
            var sev = args.Severity == XmlSeverityType.Error ? "Error" : "Warning";
            diagnostics.Add($"{sev}: {args.Message}{loc}");
            if (args.Severity == XmlSeverityType.Error)
                hadSchemaError = true;
        };

        // Data structures for logical (post-schema) checks
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var candidateRefs = new List<(string Raw, int? Line, int? Col, string? Prefix)>();
        // Expanded attribute names that hold references (best-effort list)
        var refAttributeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "sourceRef","targetRef","default","defaultFlow","messageRef","signalRef","errorRef",
            "escalationRef","itemRef","operationRef","processRef","calledElementRef","calledElement",
            "dataStoreRef","dataInputRef","dataOutputRef","loopDataInputRef","loopDataOutputRef",
            "inputRef","outputRef","activityRef","bpmnElement","categoryValueRef","interfaceRef",
            "endPointRef","compensationRef","attachedToRef"
        };

        try
        {
            using var sr = new StringReader(xml);
            using var reader = XmlReader.Create(sr, settings);
            var lineInfo = reader as IXmlLineInfo;

            int elementCount = 0;
            int depth = 0;
            int maxDepth = 0;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    elementCount++;
                    if (!reader.IsEmptyElement)
                    {
                        depth++;
                        if (depth > maxDepth) maxDepth = depth;
                    }

                    if (reader.HasAttributes)
                    {
                        for (int i = 0; i < reader.AttributeCount; i++)
                        {
                            reader.MoveToAttribute(i);
                            var attrName = reader.Name;
                            var attrValue = reader.Value;

                            if (attrName.Equals("id", StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(attrValue))
                            {
                                if (!ids.Add(attrValue))
                                    duplicates.Add(attrValue);
                            }

                            if (refAttributeNames.Contains(attrName) &&
                                !string.IsNullOrWhiteSpace(attrValue) &&
                                !IsExpression(attrValue))
                            {
                                string? prefix = null;
                                var colon = attrValue.IndexOf(':');
                                if (colon > 0 && colon < attrValue.Length - 1)
                                    prefix = attrValue[..colon];

                                candidateRefs.Add((
                                    attrValue,
                                    lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : (int?)null,
                                    lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : (int?)null,
                                    prefix
                                ));
                            }
                        }
                        reader.MoveToElement();
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (depth > 0) depth--;
                }
            }

            if (elementCount == 0)
                diagnostics.Add("Warning: Document contained zero elements (likely invalid BPMN).");

            foreach (var d in duplicates)
                diagnostics.Add($"Error: Duplicate @id '{d}'.");

            // Post-pass: attempt to resolve references
            var unresolved = new List<(string Raw, int? Line, int? Col)>();
            foreach (var c in candidateRefs)
            {
                if (ResolveReference(c.Raw, ids)) continue;
                unresolved.Add((c.Raw, c.Line, c.Col));
            }

            foreach (var (raw, line, col) in unresolved)
                diagnostics.Add($"Info: Reference '{raw}' did not match any declared @id (line {line}, pos {col}).");

            _logger.LogDebug(
                "Schema validation summary: elements={ElementCount}, maxDepth={MaxDepth}, ids={IdCount}, duplicates={DupCount}, unresolvedRefs={Unresolved}",
                elementCount, maxDepth, ids.Count, duplicates.Count, unresolved.Count);

            if (hadSchemaError)
                _logger.LogWarning("Schema validation found blocking schema errors.");
        }
        catch (XmlException xex)
        {
            diagnostics.Add($"Error: XML well-formedness error: {xex.Message} (line {xex.LineNumber}, pos {xex.LinePosition})");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Error: Unexpected validation failure: {ex.Message}");
        }

        // Local helpers
        static bool IsExpression(string v)
            => (v.StartsWith("${", StringComparison.Ordinal) && v.EndsWith("}", StringComparison.Ordinal)) ||
               (v.StartsWith("#{", StringComparison.Ordinal) && v.EndsWith("}", StringComparison.Ordinal));

        static bool ResolveReference(string raw, HashSet<string> ids)
        {
            if (ids.Contains(raw)) return true;

            var colon = raw.IndexOf(':');
            if (colon > 0 && colon < raw.Length - 1)
            {
                // Best-effort: fallback to local part
                var local = raw[(colon + 1)..];
                if (ids.Contains(local)) return true;
            }
            return false;
        }
    }

    public static Definitions Read(XDocument doc)
    {
        var bpmn = Ns.BPMN;
        var root = doc.Root ?? throw new InvalidOperationException("Missing definitions");

        // Clear raw reference caches for this document
        _rawSequenceFlowRefs.Clear();
        _rawBoundaryAttachRefs.Clear();

        var defs = new Definitions{Id = root.Attr("id"),
            TargetNamespace = root.Attr("targetNamespace") ?? "http://example.com"};
        // Optional: capture definitions @name as exporter placeholder (additive)
        var defsName = root.Attr("name");
        if (!string.IsNullOrWhiteSpace(defsName))
            defs.Exporter = defsName; // reuse property to persist original name without model change

        foreach (var imp in root.Elements("import".B()))
            defs.Imports.Add(new Import()
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
                    var idef1 = new ItemDefinition
                    {
                        Id = el.Attr("id"),
                        StructureRef = new XmlQualifiedName(el.Attr("structureRef"), null),
                        IsCollection = el.Attr("isCollection") is string value && bool.TryParse(value, out var bc) && bc
                    };
                    defs.RootElements.Add(idef1); if (idef1.Id is not null) idMap[idef1.Id] = idef1;
                    break;

                case "message":
                    var msg = new Message { Id = el.Attr("id"), Name = el.Attr("name") };
                    defs.RootElements.Add(msg); if (msg.Id is not null) idMap[msg.Id] = msg;
                    break;

                case "resource":
                    var res = new Resource(el.Attr("name") ?? "", null)
                    {
                        Id = el.Attr("id")
                    };
                    defs.RootElements.Add(res); if (res.Id is not null) idMap[res.Id] = res;
                    break;

                case "category":
                    var cat = new Category { Id = el.Attr("id"), Name = el.Attr("name") };
                    foreach (var cv in el.Elements("categoryValue".B()))
                    {
                        var v = new CategoryValue { Id = cv.Attr("id"), Value = cv.Attr("value") };
                        cat.CategoryValues.Add(v); if (v.Id is not null) idMap[v.Id] = v;
                    }
                    defs.RootElements.Add(cat); if (cat.Id is not null) idMap[cat.Id] = cat;
                    break;

                case "error":
                    var err = new Error
                    {
                        Id = el.Attr("id"),
                        Name = el.Attr("name"),
                        ErrorCode = el.Attr("errorCode"),
                        StructureRef = new XmlQualifiedName(el.Attr("structureRef"), el.Attr("ns") ?? "")
                    };
                    defs.RootElements.Add(err); if (err.Id is not null) idMap[err.Id] = err;
                    break;

                case "escalation":
                    var esc = new Escalation
                    {
                        Id = el.Attr("id"),
                        Name = el.Attr("name"),
                        EscalationCode = el.Attr("escalationCode"),
                        StructureRef = new XmlQualifiedName(el.Attr("structureRef"), el.Attr("ns") ?? "")
                    };
                    defs.RootElements.Add(esc); if (esc.Id is not null) idMap[esc.Id] = esc;
                    break;

                case "interface":

                    var ops = new List<Operation>();
                    foreach (var op in el.Elements("operation".B()))
                        ops.Add(new Operation(
                            Name: op.Attr("name") ?? "",
                            InMessageRef: null
                        )
                        {
                            Id = op.Attr("id") ?? ""
                        });
                    var i = new Interface(Name: el.Attr("name") ?? "", ops)
                    {
                        Id = el.Attr("id") ?? "",
                        ImplementationRef = new XmlQualifiedName(el.Attr("implementationRef") ?? "", el.Attr("ns") ?? "")
                    };

                    defs.RootElements.Add(i); if (i.Id is not null) idMap[i.Id] = i;
                    break;

                case "signal":
                    var s = new Signal { Id = el.Attr("id"), Name = el.Attr("name") };
                    defs.RootElements.Add(s); if (s.Id is not null) idMap[s.Id] = s;
                    break;

                case "process":
                    var p = new Process { Id = el.Attr("id"), Name = el.Attr("name"), IsExecutable = el.AttrBool("isExecutable") ?? false };
                    defs.RootElements.Add(p); if (p.Id is not null) idMap[p.Id] = p;
                    break;

                case "collaboration":
                    var c = new Collaboration { Id = el.Attr("id") };
                    defs.RootElements.Add(c); if (c.Id is not null) idMap[c.Id] = c;
                    break;

                case "choreography":
                    var ch = new Choreography { Id = el.Attr("id") };
                    defs.RootElements.Add(ch); if (ch.Id is not null) idMap[ch.Id] = ch;
                    break;

                case "relationship":
                    var rel = new Relationship {
                        Type = el.Attr("type") ?? "",
                        Direction = RelationshipDirection.None,
                        Sources = [],
                        Targets = [],
                        Id = el.Attr("id")
                    };
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
                    var iref = new XmlQualifiedName(el.Attr("itemRef"), el.Attr("ns") ?? "");
                    idMap[msg.Id!] = msg with { ItemRef = iref };
                    break;

                case "interface":
                    var iface = (Interface)idMap[el.Attr("id")!];
                    foreach (var opEl in el.Elements("operation".B()))
                    {
                        var op = iface.Operations.First(x => x.Id == opEl.Attr("id"));
                        var inMsg = op.InMessageRef;
                        if (opEl.Attr("inMessageRef") is string inRef)
                        {
                            inMsg = new XmlQualifiedName(inRef, null);
                        }

                        var outMsg = op.OutMessageRef;
                        if (opEl.Attr("outMessageRef") is string outRef)
                        {
                            outMsg = new XmlQualifiedName(outRef, null);
                        }
                        var errorRefs = op.ErrorRefs.ToList();
                        foreach (var er in opEl.Elements("errorRef".B()))
                            if ((string?)er is string eid && idMap.TryGetValue(eid, out var e) && e is Error err && !errorRefs.Contains(new XmlQualifiedName(eid, null))) errorRefs.Add(new XmlQualifiedName(eid, null));
                        // Replace the operation in the list with a new instance with updated properties
                        var updatedOp = op with
                        {
                            ImplementationRef = new XmlQualifiedName(opEl.Attr("implementationRef"), null),
                            InMessageRef = inMsg,
                            OutMessageRef = outMsg,
                            ErrorRefs = errorRefs
                        };
                        iface.Operations.Add(updatedOp);
                    }
                    break;

                case "process":
                    var p = (Process)idMap[el.Attr("id")!];

                    foreach (var child in el.Elements())
                    {
                        if (child.Name == "ioSpecification".B()) {
                            p = p with {IoSpecification = ReadIOSpec(child)}; continue; }

                        if (child.Name == "laneSet".B())
                        {
                            var ls = new LaneSet { Id = child.Attr("id"), Name = child.Attr("name") };
                            foreach (var ln in child.Elements("lane".B()))
                            {
                                var lane = new Lane { 
                                    Name = ln.Attr("name") ?? "",
                                    Id = ln.Attr("id"),
                                    ChildLaneSet = new LaneSet { Id = ln.Attr("childLaneSet") ?? null },
                                    PartitionElement =   null,
                                    PartitionElementRef = new XmlQualifiedName(ln.Attr("partitionElementRef") ?? null ),
                                };
                                foreach (var fnr in ln.Elements("flowNodeRef".B()))
                                    if ((string?)fnr is string id && idMap.TryGetValue(id, out var fn) && fn is FlowNode fnode) lane.FlowNodeRefs.Add(fnode.Id);
                                ls.Lanes.Add(lane);
                            }
                            p.LaneSets.Add(ls);
                            continue;
                        }

                        var fe = ReadFlowElement(child, idMap);
                        if (fe != null)
                            p.FlowElements.Add(fe);

                        // Additive: parse data-related elements that are flow-scoped but not handled by ReadFlowElement
                        if (child.Name.LocalName == "dataObject" && child.Name.Namespace == bpmn)
                        {
                            var dobj = new DataObject { Id = child.Attr("id"), Name = child.Attr("name"), IsCollection = child.AttrBool("isCollection") == true };
                            if (dobj.Id is not null) idMap[dobj.Id] = dobj;
                            p.FlowElements.Add(dobj);
                        }
                        else if (child.Name.LocalName == "dataObjectReference" && child.Name.Namespace == bpmn)
                        {
                            var refId = child.Attr("dataObjectRef");
                            var itemSubjectRef = new XmlQualifiedName( child.Attr("itemSubjectRef"), null);
                            var dataState = new DataState { Name = child.Attr("dataState") };
                            var dref =  new DataObjectReference { Id = child.Attr("id"), DataObjectRef = refId, ItemSubjectRef = itemSubjectRef, DataState = dataState } ;
                            if (dref is not null && dref.Id is not null) { idMap[dref.Id] = dref; p.FlowElements.Add(dref); }
                        }
                        else if (child.Name.LocalName == "dataStore" && child.Name.Namespace == bpmn)
                        {
                            var itemSubjectRef = new XmlQualifiedName(child.Attr("itemSubjectRef"), null);
                            var dataState = new DataState { Name = child.Attr("dataState") };
                            var isUnlimited = child.AttrBool("isUnlimited") ?? false;
                            var ds = new DataStore() { Id = child.Attr("id") , Name = child.Attr("name") ?? "",
                                IsUnlimited = isUnlimited,
                                Capacity = child.Attr("capacity"),
                                DataState = dataState, ItemSubjectRef = itemSubjectRef};
                            if (ds.Id is not null) idMap[ds.Id] = ds; // do not add to FlowElements (not a FlowElement)
                        }
                        else if (child.Name.LocalName == "dataStoreReference" && child.Name.Namespace == bpmn)
                        {
                            var refId = child.QualifiedName("dataStoreRef");
                            var itemSubjectRef = child.QualifiedName("itemSubjectRef");
                            var dsr =  new DataStoreReference() { Id = child.Attr("id"), DataStoreRef = refId, ItemSubjectRef = itemSubjectRef } ;
                             idMap[dsr.Id] = dsr; p.FlowElements.Add(dsr);
                        }

                        // Data associations at process scope activities
                        if (fe is Activity actRoot)
                        {
                            var inAssocs = new List<DataInputAssociation>();
                            foreach (var dia in child.Elements("dataInputAssociation".B()))
                            {
                                var sources = new List<string>();
                                foreach (var sr in dia.Elements("sourceRef".B()))
                                    if (!string.IsNullOrWhiteSpace(sr.Value)) sources.Add(sr.Value);
                                var targetRef = dia.Element("targetRef".B())?.Value;
                                inAssocs.Add(new DataInputAssociation { SourceRefs = sources, TargetRef = targetRef });
                            }
                            var outAssocs = new List<DataOutputAssociation>();
                            foreach (var doa in child.Elements("dataOutputAssociation".B()))
                            {
                                var sourceRefs = new List<string>();
                                foreach (var sr in doa.Elements("sourceRef".B()))
                                    if (!string.IsNullOrWhiteSpace(sr.Value)) sourceRefs.Add(sr.Value);
                                var targetRef = doa.Element("targetRef".B())?.Value;
                                    outAssocs.Add(new DataOutputAssociation { SourceRefs = sourceRefs, TargetRef = targetRef });
                            }
                            if (inAssocs.Count > 0 || outAssocs.Count > 0)
                            {
                                var updated = actRoot with {  DataInputAssociations = inAssocs, DataOutputAssociations = outAssocs };
                                // replace in FlowElements
                                p.FlowElements[p.FlowElements.Count - 1] = updated;
                                if (updated.Id is not null) idMap[updated.Id] = updated;
                            }
                        }
                    }
                    break;

                case "collaboration":
                    var c = (Collaboration)idMap[el.Attr("id")!];
                    foreach (var child in el.Elements())
                    {
                        switch (child.Name.LocalName)
                        {
                            case "participant":
                                var part = new Participant { 
                                    Name = child.Attr("name") ?? "",
                                    Id = child.Attr("id"),
                                    ProcessRef =  child.QualifiedName("processRef"),
                                    EndPointRefs = child.QualifiedNames("endPointRefs"),
                                    InterfaceRefs = child.QualifiedNames("interfaceRefs"),
                                };
                                c.Participants.Add(part);
                                break;

                            // Replace the instantiation of MessageFlow in the "collaboration" case with the required constructor arguments
                            case "messageFlow":
                                var mf = new MessageFlow { 
                                    Name= child.Attr("name"),
                                    SourceRef = child.QualifiedName("sourceRef"),
                                    TargetRef = child.QualifiedName("targetRef"),
                                    MessageRef = child.QualifiedName("messageRef"),
                                    Id = child.Attr("id") };
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

            var planeBpmn = planeEl.QualifiedName("bpmnElement");
            var plane = new BpmnPlane() { Id = planeEl.Attr("id"), BpmnElement = planeBpmn };

            foreach (var s in planeEl.Elements("BPMNShape".BPMNDI()))
            {
                var bRef = s.QualifiedName("bpmnElement");
                var shape = new BpmnShape(){ Id = s.Attr("id"),  BpmnElement = bRef };
                var b = s.Element("Bounds".DC());
                if (b is not null)
                    shape.Bounds = new Bounds(
                        double.Parse(b.Attr("x") ?? "0"),
                        double.Parse(b.Attr("y") ?? "0"),
                        double.Parse(b.Attr("width") ?? "0"),
                        double.Parse(b.Attr("height") ?? "0"));
                plane.DiagramElements.Add(shape);
            }

            foreach (var e in planeEl.Elements("BPMNEdge".BPMNDI()))
            {
                var bpmnElement = e.QualifiedName("bpmnElement");
                var sourceElement = e.QualifiedName("sourceElement");
                var targetElement = e.QualifiedName("targetElement");
               
                var label = new BpmnLabel { LabelStyle = e.QualifiedName("labelStyle")  };
                var b = e.Element("Bounds".DC());
                if (b is not null)
                    label.Bounds = new Bounds(
                        double.Parse(b.Attr("x") ?? "0"),
                        double.Parse(b.Attr("y") ?? "0"),
                        double.Parse(b.Attr("width") ?? "0"),
                        double.Parse(b.Attr("height") ?? "0"));
                var edge = new BpmnEdge { Id = e.Attr("id"),  BpmnElement = bpmnElement, BpmnLabel = label };
                foreach (var wp in e.Elements("waypoint".DI()))
                    edge.Waypoints.Add(new Point(double.Parse(wp.Attr("x") ?? "0"), double.Parse(wp.Attr("y") ?? "0")));

                plane.DiagramElements.Add(edge);
            }
           var bpmnLabelStyles = new List<BpmnLabelStyle>();
            foreach (var s in xdiag.Elements("BPMNLabelStyle".BPMNDI()))
            {
                var bpmnLabelStyle = new BpmnLabelStyle() { Id = s.Attr("id") };
                var b = s.Element("Bounds".DC());
                if (b is not null)
                    bpmnLabelStyle.Font = new Font(b.Attr("name"),
                        b.AttrDouble("size"),
                        b.AttrBool("isBold").Value,
                        b.AttrBool("isItalic").Value,
                        b.AttrBool("isUnderline").Value,
                        b.AttrBool("isStrikeThrough").Value
                        );
                bpmnLabelStyles.Add(bpmnLabelStyle);
            }

            defs.BpmnDiagrams.Add(new BpmnDiagram { 
                Name= xdiag.Attr("name") ?? "",
                BpmnPlane = plane,
                BpmnLabelStyles = bpmnLabelStyles,
                Id = xdiag.Attr("id") });
        }

        // Forward reference fix-up (sequenceFlow source/target, boundaryEvent attachment)
        ResolveForwardReferences(defs, idMap);

        return defs;
    }

    private static void ResolveForwardReferences(Definitions defs, Dictionary<string, BaseElement> idMap)
    {
        foreach (var proc in defs.RootElements.OfType<Process>())
        {
            for (int i = 0; i < proc.FlowElements.Count; i++)
            {
                switch (proc.FlowElements[i])
                {
                    case SequenceFlow sf when sf.Id is not null && _rawSequenceFlowRefs.TryGetValue(sf.Id, out var raw):
                        var updated = sf;
                        if (sf.SourceRef == null && raw.SourceRef is string sId && idMap.TryGetValue(sId, out var sEl) && sEl is FlowNode sNode)
                            updated = updated with { SourceRef = sNode.Name };
                        if (sf.TargetRef == null && raw.TargetRef is string tId && idMap.TryGetValue(tId, out var tEl) && tEl is FlowNode tNode)
                            updated = updated with { TargetRef = tNode.Name };
                        if (!ReferenceEquals(updated, sf))
                        {
                            proc.FlowElements[i] = updated;
                            idMap[sf.Id] = updated;
                        }
                        break;
                    case BoundaryEvent be when be.Id is not null && be.AttachedToRef == null && _rawBoundaryAttachRefs.TryGetValue(be.Id, out var attId) && attId is not null:
                        if (idMap.TryGetValue(attId, out var aEl) && aEl is Activity act)
                        {
                            var updatedBe = be with { AttachedToRef = new XmlQualifiedName( act.Name) };
                            proc.FlowElements[i] = updatedBe;
                            idMap[be.Id] = updatedBe;
                        }
                        break;
                }
            }
        }
    }

    static InputOutputSpecification ReadIOSpec(XElement x)
    {
        var io = new InputOutputSpecification { Id = x.Attr("id") };
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
                if ((string?)r is string id && inMap.TryGetValue(id, out var d)) s.DataInputRefs.Add(d.
                    Id);
            io.InputSets.Add(s);
        }
        foreach (var set in x.Elements("outputSet".B()))
        {
            var s = new OutputSet { Id = set.Attr("id") };
            foreach (var r in set.Elements("dataOutputRef".B()))
                if ((string?)r is string id && outMap.TryGetValue(id, out var d)) s.DataOutputRefs.AddRange(d.Name);
            io.OutputSets.Add(s);
        }
        return io;
    }

    static FlowElement? ReadFlowElement(XElement x, Dictionary<string, BaseElement> idMap)
    {
        FlowElement? fe = x.Name.LocalName switch
        {
            // Activities / Tasks
            "task" => new Bpmn.Task { Id = x.Attr("id"), Name = x.Attr("name"), AnyAttributes = [
                    x.XmlAttribute("isForCompensation"),
                    x.XmlAttribute("startQuantity"),
                    x.XmlAttribute("completionQuantity")
                ]
            },
            // ServiceTask: correct BPMN attribute is 'implementation' (lowercase)
            "serviceTask" => new ServiceTask { Id = x.Attr("id"), Name = x.Attr("name"), Implementation = x.Attr("implementation") },
            "userTask" => new UserTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "scriptTask" => new ScriptTask { 
                ScriptFormat = x.Attr("scriptFormat") ?? "",
                Script = new Script(x.Element("script".B()) is XElement scriptEl && !string.IsNullOrEmpty(scriptEl.Value)
                        ? new[] { scriptEl.Value }
                        : Array.Empty<string>(), x.XmlElement("any".B())
                )
            },
            "manualTask" => new ManualTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "businessRuleTask" => new BusinessRuleTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "sendTask" => new SendTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "receiveTask" => new ReceiveTask { Id = x.Attr("id"), Name = x.Attr("name"), Implementation = x.Attr("implementation"), Instantiate = x.AttrBool("instantiate") ?? false },
            "callActivity" => new CallActivity
            {
                Id = x.Attr("id"),
                // Normalize both attributes: prefer calledElementRef, fallback calledElement
                CalledElement = x.QualifiedName("calledElement") 
            },

            // Subprocess
            "subProcess" => new SubProcess
            {
                Id = x.Attr("id"),
                TriggeredByEvent = x.AttrBool("triggeredByEvent") ?? false,
                LoopCharacteristics = new MultiInstanceLoopCharacteristics { Id = x.Attr("multiInstanceLoopCharacteristics".B()) }
            },
            "transaction" => new Transaction(x.Attr("method") ?? string.Empty) { Id = x.Attr("id") },
            "adHocSubProcess" => new AdHocSubProcess()
            {
                Id = x.Attr("id"),
                CompletionCondition = null!,
                Ordering = default,
                CancelRemainingInstances = false
            },

            // Gateways
            "exclusiveGateway" => new ExclusiveGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "inclusiveGateway" => new InclusiveGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "parallelGateway" => new ParallelGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "complexGateway" => new ComplexGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "eventBasedGateway" => new EventBasedGateway { Id = x.Attr("id"), Name = x.Attr("name"), Instantiate = x.AttrBool("instantiate") ?? false },

            // Events
            "startEvent" => new StartEvent { Id = x.Attr("id"), IsInterrupting = x.AttrBool("isInterrupting") ?? false, Name = x.Attr("name") ?? "startEvent" },
            "endEvent" => new EndEvent { Id = x.Attr("id"), Name = x.Attr("name") ?? "endEvent" },
            "intermediateCatchEvent" => new IntermediateCatchEvent { Id = x.Attr("id") },
            "intermediateThrowEvent" => new IntermediateThrowEvent { Id = x.Attr("id") },
            "boundaryEvent" => new BoundaryEvent { 
               AttachedToRef = new XmlQualifiedName( x.Attr("attachedToRef"), null),
               CancelActivity = x.AttrBool("cancelActivity").Value,
                Id = x.Attr("id"),
                Name = x.Attr("name")
            },

            // SequenceFlow / Artifacts
            "sequenceFlow" => new SequenceFlow()
            { Id = x.Attr("id"), Name = x.Attr("name")},


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
                if (be.AttachedToRef == null && att is not null)
                {
                    // store raw for later resolution
                    if (be.Id is not null) _rawBoundaryAttachRefs[be.Id] = att;
                }
                if (att is not null && idMap.TryGetValue(att, out var bae) && bae is FlowElement fo && fo is Activity act)
                    be = be with { AttachedToRef = new XmlQualifiedName(act.Id) };
                foreach (var ed in x.Elements()) be.EventDefinitions.Add(ReadEventDefinition(ed, idMap));
                break;
            case SubProcess sp:
                foreach (var child in x.Elements())
                {
                    // Capture loop cardinality textual body if present (multiInstanceLoopCharacteristics)
                    if (child.Name.LocalName == "multiInstanceLoopCharacteristics")
                    {
                        var loopCard = child.Element("loopCardinality".B())?.Value;
                        var isSequential = child.AttrBool("isSequential") ?? false;
                        var completionCondition = child.Element("completionCondition".B())?.Value;
                        if (!string.IsNullOrWhiteSpace(loopCard))
                        {
                            // store multi-instance characteristics
                            var miLoop = new MultiInstanceLoopCharacteristics {
                                LoopCardinality = new FormalExpression { Text = [loopCard] },
                                IsSequential= isSequential,
                                Behavior = MultiInstanceFlowCondition.All,
                                CompletionCondition = string.IsNullOrWhiteSpace(completionCondition) ? null : new FormalExpression { Text = [completionCondition] }
                            };
                            sp = sp with { LoopCharacteristics = miLoop };
                        }
                    }
                    else if (child.Name.LocalName == "standardLoopCharacteristics")
                    {
                        var loopCond = child.Element("loopCondition".B())?.Value;
                        var testBefore = child.AttrBool("testBefore") ?? false;
                        var maxStr = child.Attr("loopMaximum");
                        int? max = null;
                        if (int.TryParse(maxStr, out var mv)) max = mv;
                        if (!string.IsNullOrWhiteSpace(loopCond) || testBefore || max.HasValue)
                        {
                            var stdLoop = new StandardLoopCharacteristics();
                            sp = sp with { LoopCharacteristics = stdLoop };
                        }
                    }
                    // Data associations for activities inside subprocess
                    if (child.Name.LocalName == "dataInputAssociation" || child.Name.LocalName == "dataOutputAssociation")
                    {
                        // handled below after activity creation
                    }
                    var cfe = ReadFlowElement(child, idMap);
                    if (cfe is not null) sp.FlowElements.Add(cfe);
                    if (cfe is Activity activity)
                    {
                        var inAssocs = new List<DataInputAssociation>();
                        foreach (var dia in child.Elements("dataInputAssociation".B()))
                        {
                            var sources = new List<string>();
                            foreach (var sr in dia.Elements("sourceRef".B()))
                                if (!string.IsNullOrWhiteSpace(sr.Value)) sources.Add(sr.Value);
                            var target = dia.Element("targetRef".B())?.Value;
                            inAssocs.Add(new DataInputAssociation(sources, target));
                        }
                        var outAssocs = new List<DataOutputAssociation>();
                        foreach (var doa in child.Elements("dataOutputAssociation".B()))
                        {
                            var sources = new List<string>();
                            foreach (var sr in doa.Elements("sourceRef".B()))
                                if (!string.IsNullOrWhiteSpace(sr.Value)) sources.Add(sr.Value);
                            var target = doa.Element("targetRef".B())?.Value;
                            outAssocs.Add(new DataOutputAssociation(sources, target));
                        }
                        if (inAssocs.Count > 0 || outAssocs.Count > 0)
                        {
                            var updatedAct = activity with { DataInputAssociations = inAssocs, DataOutputAssociations = outAssocs };
                            sp.FlowElements[sp.FlowElements.Count - 1] = updatedAct;
                            if (updatedAct.Id is not null) idMap[updatedAct.Id] = updatedAct;
                        }
                        foreach (var doa in child.Elements("artifact".B()))
                        {
                            Artifact ar = ReadArtifactElement(child, idMap);
                            if (ar.Id is not null) idMap[ar.Id] = ar;
                        }

                        break;
                    }
                }
                break;
            case SequenceFlow sf:
                var rawSource = x.Attr("sourceRef");
                var rawTarget = x.Attr("targetRef");
                if (sf.Id is not null) _rawSequenceFlowRefs[sf.Id] = (rawSource, rawTarget);
                var cond = x.Element("conditionExpression".B());
                if (cond is not null)
                    sf = sf with { ConditionExpression = new FormalExpression { Text = [cond.Value] },
                        SourceRef = rawSource,
                        TargetRef = rawTarget
                    };
                break;
        }

        // Parse <incoming> / <outgoing> textual references for FlowNodes (tasks, events, gateways)
        if (fe is FlowNode fn)
        {
            var incomingIds = x.Elements("incoming".B()).Select(e => e.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var outgoingIds = x.Elements("outgoing".B()).Select(e => e.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (incomingIds.Count > 0 || outgoingIds.Count > 0)
            {
                var inFlowNames = new List<XmlQualifiedName>();
                foreach (var id in incomingIds)
                    inFlowNames.Add(new XmlQualifiedName (id));
                var outFlowNames = new List<XmlQualifiedName>();
                foreach (var id in outgoingIds)
                    outFlowNames.Add(new XmlQualifiedName(id));
                fe = fn with { Incomings = inFlowNames, Outgoings = outFlowNames };
                if (fe.Id is not null) idMap[fe.Id] = fe; // refresh mapping with updated collections
            }
        }

        return fe;
    }

    static Artifact? ReadArtifactElement(XElement x, Dictionary<string, BaseElement> idMap)
    {
        Artifact? ar = x.Name.LocalName switch
        {
            "textAnnotation" => new TextAnnotation
            {
                Text = new TText { Text = new[] { x.Element("text".B())?.Value } },
                TextFormat = x.Attr("textFormat"),
                Id = x.Attr("id")
            },

            "association" => new Association
            {
                SourceRef = x.QualifiedName("sourceRef"),
                TargetRef = x.QualifiedName("targetRef"),
                AssociationDirection = Enum.TryParse<AssociationDirection>(x.Attr("associationDirection"), out var dir) ? dir : AssociationDirection.None,
                Id = x.Attr("id")
            },

            "group" => new Group
            {
                Id = x.Attr("id"),
                CategoryValueRef = x.QualifiedName("categoryValueRef")
            },

            _ => null
        };
        return ar;
    }

    static EventDefinition ReadEventDefinition(XElement x, Dictionary<string, BaseElement> idMap)
        => x.Name.LocalName switch
        {
            "timerEventDefinition" => new TimerEventDefinition
            {
                TimeDate = x.Element("timeDate".B()) is XElement td ? new FormalExpression { Text = [td.Value] } : null,
                TimeDuration = x.Element("timeDuration".B()) is XElement tdu ? new FormalExpression { Text = [tdu.Value] } : null,
                TimeCycle = x.Element("timeCycle".B()) is XElement tc ? new FormalExpression { Text = [tc.Value] } : null,
            },
            "messageEventDefinition" => new MessageEventDefinition { MessageRef = x.QualifiedName("messageRef") },
            "errorEventDefinition" => new ErrorEventDefinition { ErrorRef = x.QualifiedName("errorRef")  },
            "escalationEventDefinition" => new EscalationEventDefinition { EscalationRef = x.QualifiedName("escalationRef") },
            "conditionalEventDefinition" => new ConditionalEventDefinition(
                x.Element("condition".B()) is XElement c ? new FormalExpression { Text = [c.Value] } : null!
            ),
            "linkEventDefinition" => new LinkEventDefinition(
                x.Attr("name") ?? string.Empty,
                x.QualifiedNames("source"),
                x.QualifiedName("target")
            ),
            "signalEventDefinition" => new SignalEventDefinition { SignalRef = x.QualifiedName("signalRef")  },
            "cancelEventDefinition" => new CancelEventDefinition(),
            "compensateEventDefinition" => new CompensateEventDefinition { ActivityRef = x.QualifiedName("activityRef") },
            "terminateEventDefinition" => new TerminateEventDefinition(),
            _ => null
        };

    public static BpmnModel ToModel(Definitions definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));

        var model = new BpmnModel
        {
            ProcessId = string.Empty,
            Name = string.Empty,
            Events = new List<Event>(),
            Gateways = new List<Gateway>(),
            Subprocesses = new List<SubProcess>(),
            SequenceFlows = new List<SequenceFlow>(),
            Tasks = new List<Bpmn.Task>(),
            DataObjects = new List<DataObject>(),
            DataObjectReferences = new List<DataObjectReference>(),
            DataStores = new List<DataStore>(),
            DataStoreReferences = new List<DataStoreReference>(),
            Properties = new List<Property>(),
            ActivityIo = [],
            Messages = new List<Message>(),
            Signals = new List<Signal>(),
            Errors = new List<Error>(),
            Escalations = new List<Escalation>(),
            Diagnostics = new List<string>(),
            Shapes = new List<BpmnShape>(),
            Edges = new List<BpmnEdge>(),
            Participants = new List<Participant>(),
            Lanes = new List<Lane>(),
            MessageFlows = new List<MessageFlow>(),
            TextAnnotations = new List<TextAnnotation>(),
            Associations = new List<Association>(),
            ProcessVariables = new Dictionary<string, object>(),
            Activities = new List<Activity>(),
            //Definitions  = new List<Definitions>(),
            ProcessDefinitions = definitions
        };

        // Process RootElements to extract process-specific elements
        foreach (var rootElement in definitions.RootElements)
        {
            switch (rootElement)
            {
                case Process process:
                    ProcessProcess(process, model);
                    break;
                case Message message:
                    model = model with { Messages = model.Messages.Append(message).ToList() };
                    break;
                case Signal signal:
                    model = model with { Signals = model.Signals.Append(signal).ToList() };
                    break;
                case Error error:
                    model = model with { Errors = model.Errors.Append(error).ToList() };
                    break;
                case Escalation escalation:
                    model = model with { Escalations = model.Escalations.Append(escalation).ToList() };
                    break;
                case Collaboration collaboration:
                    ProcessCollaboration(collaboration, model);
                    break;
            }
        }

        // Process Diagrams to extract shapes and edges
        foreach (var diagram in definitions.BpmnDiagrams)
        {
            ProcessDiagram(diagram, model);
        }

        return model;
    }

    private static void ProcessProcess(Process process, BpmnModel model)
    {
        // Set process-level information
        if (string.IsNullOrEmpty(model.ProcessId))
        {
            model.ProcessId = process.Id ?? string.Empty;
        }

        if (string.IsNullOrEmpty(model.Name))
        {
            model.Name = process.Name ?? string.Empty;
        }

        // Add process properties
        if (process.Properties != null)
        {
            model = model with { Properties = model.Properties.Concat(process.Properties).ToList() };
        }

        // Add process lanes
        if (process.LaneSets != null)
        {
            foreach (var laneSet in process.LaneSets)
            {
                if (laneSet.Lanes != null)
                {
                    model = model with { Lanes = model.Lanes.Concat(laneSet.Lanes).ToList() };
                }
            }
        }

        // Process flow elements
        if (process.FlowElements != null)
        {
            foreach (var flowElement in process.FlowElements)
            {
                switch (flowElement)
                {
                    case Bpmn.Task task:
                        model = model with { Tasks = model.Tasks.Append(task).ToList() };
                        model = model with { ActivityIo = model.ActivityIo.Append(task.IoSpecification).ToList() };
                        break;
                    case Event eventElement:
                        model = model with { Events = model.Events.Append(eventElement).ToList() };
                        break;
                    case Gateway gateway:
                        model = model with { Gateways = model.Gateways.Append(gateway).ToList() };
                        break;
                    case SequenceFlow sequenceFlow:
                        model = model with { SequenceFlows = model.SequenceFlows.Append(sequenceFlow).ToList() };
                        break;
                    case SubProcess subProcess:
                        model = model with { Subprocesses = model.Subprocesses.Append(subProcess).ToList() };
                        model = model with { ActivityIo = model.ActivityIo.Append(subProcess.IoSpecification).ToList() };
                        break;
                    case DataObject dataObject:
                        model = model with { DataObjects = model.DataObjects.Append(dataObject).ToList() };
                        break;
                    case DataObjectReference dataObjectRef:
                        model = model with { DataObjectReferences = model.DataObjectReferences.Append(dataObjectRef).ToList() };
                        break;
                    case DataStoreReference dataStoreRef:
                        model = model with { DataStoreReferences = model.DataStoreReferences.Append(dataStoreRef).ToList() };
                        break;
                }
            }
            
        }

        // Process artifacts
        if (process.Artifacts != null)
        {
            foreach (var artifact in process.Artifacts)
            {
                switch (artifact)
                {
                    case TextAnnotation textAnnotation:
                        model = model with { TextAnnotations = model.TextAnnotations.Append(textAnnotation).ToList() };
                        break;
                    case Association association:
                        model = model with { Associations = model.Associations.Append(association).ToList() };
                        break;
                    case Group group:
                        model = model with { Groups = model.Groups.Append(group).ToList() };
                        break;
                }
            }
        }
    }

    private static void ProcessCollaboration(Collaboration collaboration, BpmnModel model)
    {
        // Add participants
        if (collaboration.Participants != null)
        {
            model = model with { Participants = model.Participants.Concat(collaboration.Participants).ToList() };
        }

        // Add message flows
        if (collaboration.MessageFlows != null)
        {
            model = model with { MessageFlows = model.MessageFlows.Concat(collaboration.MessageFlows).ToList() };
        }

        // Add conversations (if any artifacts)
        if (collaboration.Artifacts != null)
        {
            foreach (var artifact in collaboration.Artifacts)
            {
                switch (artifact)
                {
                    case TextAnnotation textAnnotation:
                        model = model with { TextAnnotations = model.TextAnnotations.Append(textAnnotation).ToList() };
                        break;
                    case Association association:
                        model = model with { Associations = model.Associations.Append(association).ToList() };
                        break;
                }
            }
        }
    }

    private static void ProcessDiagram(BpmnDiagram diagram, BpmnModel model)
    {
        if (diagram.BpmnPlane?.DiagramElements.OfType<BpmnShape>().ToList() != null)
        {
            model = model with { Shapes = model.Shapes.Concat(diagram.BpmnPlane?.DiagramElements.OfType<BpmnShape>().ToList()).ToList() };
        }

        if (diagram.BpmnPlane?.DiagramElements.OfType<BpmnEdge>().ToList() != null)
        {
            model = model with { Edges = model.Edges.Concat(diagram.BpmnPlane?.DiagramElements.OfType<BpmnEdge>().ToList()).ToList() };
        }
    }

    //Regelbasierte semantische Validierung
    private static List<ValidationDiagnostic> RunSemanticValidation(BpmnModel model, ILogger logger)
    {
        var context = new SemanticValidationContext(model);
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var rule in SemanticRules.All)
        {
            try
            {
                var ruleDiagnostics = rule.Evaluate(model, context);
                if (ruleDiagnostics.Any())
                {
                    diagnostics.AddRange(ruleDiagnostics);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Semantic rule '{Rule}' failed", rule.GetType().Name);
                diagnostics.Add(new ValidationDiagnostic("BPMN000", ValidationSeverity.Fatal,
                    $"Semantic rule '{rule.GetType().Name}' internal error: {ex.Message}", null, "Internal"));
            }
        }

        return diagnostics;
    }
}