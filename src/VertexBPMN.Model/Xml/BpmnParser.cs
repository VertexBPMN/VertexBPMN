using DiffEngine;
using Microsoft.Diagnostics.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using VertexBPMN.Domain.Model.Bpmn.Choreography;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Diagram;
using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Exceptions;
using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Gateway;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Bpmn.Process;
using VertexBPMN.Domain.Model.Bpmn.Service;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;
using Association = VertexBPMN.Domain.Model.Bpmn.Common.Association;
using DataInput = VertexBPMN.Domain.Model.Bpmn.Process.DataInput;
using DataOutput = VertexBPMN.Domain.Model.Bpmn.Process.DataOutput;
using InputSet = VertexBPMN.Domain.Model.Bpmn.Process.InputSet;
using Operation = VertexBPMN.Domain.Model.Bpmn.Common.Operation;
using ScriptTask = VertexBPMN.Domain.Model.Bpmn.Process.ScriptTask;
using Signal = VertexBPMN.Domain.Model.Bpmn.Common.Signal;
using Task = VertexBPMN.Domain.Model.Bpmn.Process.Task;
using TextAnnotation = VertexBPMN.Domain.Model.Bpmn.Common.TextAnnotation;

namespace VertexBPMN.Domain.Model;


public class BpmnParser
{
    private readonly ILogger<BpmnParser> _logger;

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

            ValidateXmlAgainstSchemas(bpmnXml, diagnostics);
            if (diagnostics.Any(d => d.StartsWith("Error:", StringComparison.Ordinal)))
                throw new BpmnSchemaValidationException(diagnostics);

            // 2. Robust Parse mit LineInfo & Kontext
            XDocument doc;
            try
            {
                doc = XDocument.Parse(bpmnXml, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            }
            catch (XmlException ex)
            {
                var ctx = ExtractErrorContext(bpmnXml, ex.LineNumber, ex.LinePosition);
                _logger.LogError(ex, "XML parse failed at line {Line}, pos {Pos}. Context: {Context}", ex.LineNumber, ex.LinePosition, ctx);
                throw new BpmnParseException($"XML not well-formed at line {ex.LineNumber}, pos {ex.LinePosition}. Context: {ctx}", ex);
            }

            var definitions = Read(doc);
            var model = ToModel(definitions);

            model.ValidationDiagnostics = RunSemanticValidation(model, _logger);
            if (model.ValidationDiagnostics.Any())
                throw new BpmnSchemaValidationException(model.ValidationDiagnostics);
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

    public static Bpmn.Infrastructure.Definitions Read(XDocument doc)
    {
        var bpmn = Ns.BPMN;
        var root = doc.Root ?? throw new InvalidOperationException("Missing definitions");

        var defs = new Bpmn.Infrastructure.Definitions{Id = root.Attr("id"),
            TargetNamespace = root.Attr("targetNamespace") ?? "http://example.com"};

        foreach (var imp in root.Elements("import".B()))
            defs.Imports.Add(new Bpmn.Infrastructure.Import()
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
                        StructureRef = el.Attr("structureRef"),
                        IsCollection = el.Attr("isCollection") is string value && bool.TryParse(value, out var bc) && bc
                    };
                    defs.RootElements.Add(idef1); if (idef1.Id is not null) idMap[idef1.Id] = idef1;
                    break;

                case "message":
                    var msg = new Message { Id = el.Attr("id"), Name = el.Attr("name") };
                    defs.RootElements.Add(msg); if (msg.Id is not null) idMap[msg.Id] = msg;
                    break;

                case "resource":
                    var res = new Resource(
                    Name: el.Attr("name") ?? "",
                    ResourceParameters: []
                    )
                    { Id = el.Attr("id") };
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
                    var err = new Error
                    {
                        Id = el.Attr("id"),
                        Name = el.Attr("name"),
                        ErrorCode = el.Attr("errorCode"),
                        StructureRef = el.Attr("structureRef") is string stref && idMap.TryGetValue(stref, out var itdef) && itdef is ItemDefinition itemdf ? itemdf : null
                    };
                    defs.RootElements.Add(err); if (err.Id is not null) idMap[err.Id] = err;
                    break;

                case "escalation":
                    var esc = new Escalation
                    {
                        Id = el.Attr("id"),
                        Name = el.Attr("name"),
                        EscalationCode = el.Attr("escalationCode"),
                        StructureRef = el.Attr("structureRef") is string sref && idMap.TryGetValue(sref, out var idef) && idef is ItemDefinition idf ? idf : null
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
                    var i = new Interface(Name: el.Attr("name") ?? "",  ops)
                    {
                        Id = el.Attr("id") ?? "",
                        ImplementationRef = el.Attr("implementationRef") ?? ""
                    };

                    defs.RootElements.Add(i); if (i.Id is not null) idMap[i.Id] = i;
                    break;

                case "signal":
                    var s = new Signal { Id = el.Attr("id"), Name = el.Attr("name") };
                    defs.RootElements.Add(s); if (s.Id is not null) idMap[s.Id] = s;
                    break;

                case "process":
                    var p = new Process { Id = el.Attr("id"), Name = el.Attr("name"), IsExecutable = el.AttrBool("isExecutable") ?? false, Properties = [], Resources = [] };
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
                    var rel = new Relationship(
                        Type: el.Attr("type") ?? "",
                        Direction: RelationshipDirection.None,
                        Sources: [],
                        Targets: [],
                        Id: el.Attr("id")
                    );
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
                        idMap[msg.Id!] = msg with { ItemRef = idf };
                    break;

                case "interface":
                    var iface = (Interface)idMap[el.Attr("id")!];
                    foreach (var opEl in el.Elements("operation".B()))
                    {
                        var op = iface.Operations.First(x => x.Id == opEl.Attr("id"));
                        var inMsg = (opEl.Attr("inMessageRef") is string inRef && idMap.TryGetValue(inRef, out var im) && im is Message inMsgVal) ? inMsgVal : op.InMessageRef;
                        var outMsg = (opEl.Attr("outMessageRef") is string outRef && idMap.TryGetValue(outRef, out var om) && om is Message outMsgVal) ? outMsgVal : op.OutMessageRef;
                        var errorRefs = op.ErrorRefs.ToList();
                        foreach (var er in opEl.Elements("errorRef".B()))
                            if ((string?)er is string eid && idMap.TryGetValue(eid, out var e) && e is Error err && !errorRefs.Contains(err)) errorRefs.Add(err);
                        // Replace the operation in the list with a new instance with updated properties
                        var updatedOp = op with
                        {
                            ImplementationRef = opEl.Attr("implementationRef"),
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
                                var lane = new Lane(
                                    Name: ln.Attr("name") ?? "",
                                    FlowNodeRefs: [],
                                    ChildLaneSet: null,
                                    PartitionElement: null,
                                    PartitionElementRef: null
                                )
                                { Id = ln.Attr("id") };
                                foreach (var fnr in ln.Elements("flowNodeRef".B()))
                                    if ((string?)fnr is string id && idMap.TryGetValue(id, out var fn) && fn is FlowNode fnode) lane.FlowNodeRefs.Add(fnode);
                                ls.Lanes.Add(lane);
                            }
                            p.LaneSets.Add(ls);
                            continue;
                        }

                        var fe = ReadFlowElement(child, idMap);
                        if (fe != null)
                            p.FlowElements.Add(fe);
                    }
                    break;

                case "collaboration":
                    var c = (Collaboration)idMap[el.Attr("id")!];
                    foreach (var child in el.Elements())
                    {
                        switch (child.Name.LocalName)
                        {
                            case "participant":
                                var part = new Participant(
                                    Name: child.Attr("name") ?? "",
                                    ProcessRef: null,
                                    InterfaceRefs: [],
                                    EndPointRefs: [],
                                    ParticipantMultiplicity: null,
                                    PartnerRoleRef: null,
                                    PartnerEntityRef: null
                                )
                                { Id = child.Attr("id") };
                                if (child.Attr("processRef") is string pref && idMap.TryGetValue(pref, out var pe) && pe is Process pr)
                                    part = part with { ProcessRef = pr };
                                c.Participants.Add(part);
                                break;

                            // Replace the instantiation of MessageFlow in the "collaboration" case with the required constructor arguments
                            case "messageFlow":
                                var mf = new MessageFlow(
                                    Name: child.Attr("name"),
                                    SourceRef: null!,
                                    TargetRef: null!,
                                    MessageRef: null
                                )
                                { Id = child.Attr("id") };
                                if (child.Attr("sourceRef") is string sref && idMap.TryGetValue(sref, out var se) && se is InteractionNode src) mf = mf with { SourceRef = src };
                                if (child.Attr("targetRef") is string tref && idMap.TryGetValue(tref, out var te) && te is InteractionNode tgt) mf = mf with { TargetRef = tgt };
                                if (child.Attr("messageRef") is string mref && idMap.TryGetValue(mref, out var me) && me is Message m) mf = mf with { MessageRef = m };
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
            var plane = new BPMNPlane(planeRef) { Id = planeEl.Attr("id") };

            foreach (var s in planeEl.Elements("BPMNShape".BPMNDI()))
            {
                var bRef = s.Attr("bpmnElement");
                var elRef = (bRef is not null && idMap.TryGetValue(bRef, out var bel)) ? bel : null;
                var shape = new BPMNShape(elRef) { Id = s.Attr("id") };
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
                var elRef = (bRef is not null && idMap.TryGetValue(bRef, out var bel)) ? bel :null;
                var edge = new BPMNEdge(elRef, null) { Id = e.Attr("id") };
                foreach (var wp in e.Elements("waypoint".DI()))
                    edge.WayPoints.Add(new Point(double.Parse(wp.Attr("x") ?? "0"), double.Parse(wp.Attr("y") ?? "0")));
                plane.Edges.Add(edge);
            }

            defs.Diagrams.Add(new BPMNDiagram(
                Name: xdiag.Attr("name") ?? "",
                BPMNPlane: plane,
                BPMNLabelStyles: []
            ) { Id = xdiag.Attr("id") });
        }

        return defs;
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
            "serviceTask" => new ServiceTask { Id = x.Attr("id"), Name = x.Attr("name"), Implementation = x.Attr("Implementation") },
            "userTask" => new UserTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "scriptTask" => new ScriptTask(
                x.Attr("scriptFormat") ?? "",
                x.Element("script".B())?.Value ?? ""
            )
            {
                Id = x.Attr("id"),
                Name = x.Attr("name")
            },
            "manualTask" => new ManualTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "businessRuleTask" => new BusinessRuleTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "sendTask" => new SendTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "receiveTask" => new ReceiveTask { Id = x.Attr("id"), Name = x.Attr("name"), Implementation = x.Attr("Implementation"), Instantiate = x.AttrBool("instantiate") ?? false },
            "callActivity" => new CallActivity
            {
                Id = x.Attr("id"),
                // CalledElementRef must be a CallableElement, not a string.
                // We need to resolve the reference from idMap if possible, otherwise leave it null.
                CalledElementRef = x.Attr("calledElementRef") is string cref && idMap.TryGetValue(cref, out var ce) && ce is CallableElement callable ? callable : null
            },

            // Subprocess
            "subProcess" => new SubProcess { Id = x.Attr("id"), TriggeredByEvent = x.AttrBool("triggeredByEvent") ?? false },
            "transaction" => new Transaction(x.Attr("method") ?? string.Empty) { Id = x.Attr("id") },
            "adHocSubProcess" => new AdHocSubProcess(
                CompletionCondition: null!,
                Ordering: default,
                CancelRemainingInstances: false
            ) { Id = x.Attr("id") },

            // Gateways
            "exclusiveGateway" => new ExclusiveGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "inclusiveGateway" => new InclusiveGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "parallelGateway" => new ParallelGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "complexGateway" => new ComplexGateway { Id = x.Attr("id"), Name = x.Attr("name") },
            "eventBasedGateway" => new EventBasedGateway { Id = x.Attr("id"), Name = x.Attr("name"), Instantiate = x.AttrBool("instantiate") ?? false },

            // Events
            "startEvent" => new StartEvent { Id = x.Attr("id"), IsInterrupting = x.AttrBool("isInterrupting") ?? false, Name = "startEvent" },
            "endEvent" => new EndEvent { Id = x.Attr("id"), Name = "endEvent" },
            "intermediateCatchEvent" => new IntermediateCatchEvent { Id = x.Attr("id") },
            "intermediateThrowEvent" => new IntermediateThrowEvent { Id = x.Attr("id") },
            "boundaryEvent" => new BoundaryEvent(
                x.Attr("attachedToRef") is string att && idMap.TryGetValue(att, out var bae) && bae is Activity act ? act : null!,
                x.AttrBool("cancelActivity") ?? false
            )
            {
                Id = x.Attr("id"),
                Name = x.Attr("name")
            },

            // SequenceFlow / Artifacts
            "sequenceFlow" => new SequenceFlow(
                SourceRef: null!, // Will be set later in the switch(fe) block below if possible
                TargetRef: null!, // Will be set later in the switch(fe) block below if possible
                ConditionExpression: null,
                IsImmediate: false
            )
            { Id = x.Attr("id") },

            "textAnnotation" => new TextAnnotation(
                x.Element("text".B())?.Value,
                x.Attr("textFormat")
            ) { Id = x.Attr("id") },
            "association" => new Association(
                SourceRef: null!, // Will be set later in the switch(fe) block below if possible
                TargetRef: null!, // Will be set later in the switch(fe) block below if possible
                Direction: AssociationDirection.None
            ) { Id = x.Attr("id") },

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
                if (att is not null && idMap.TryGetValue(att, out var bae) && bae is FlowElement fo && fo is Activity act)
                    be = be with {AttachedToRef = act};
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
                if (x.Attr("sourceRef") is string sref && idMap.TryGetValue(sref, out var s) && s is FlowNode sn)
                    sf = sf with {SourceRef = sn};
                if (x.Attr("targetRef") is string tref && idMap.TryGetValue(tref, out var t) && t is FlowNode tn)
                    sf = sf with {TargetRef = tn};
                var cond = x.Element("conditionExpression".B());
                if (cond is not null)
                    sf = sf with {ConditionExpression = new FormalExpression {Body = cond.Value}};
                break;
            case Association a:
                if (x.Attr("sourceRef") is string asrc && idMap.TryGetValue(asrc, out var sourceRef))
                    a = a with {SourceRef = sourceRef};
                if (x.Attr("targetRef") is string atgt && idMap.TryGetValue(atgt, out var targetRef))
                    a = a with {TargetRef = targetRef};
                break;
        }

        return fe;
    }

    static EventDefinition ReadEventDefinition(XElement x, Dictionary<string, BaseElement> idMap)
        => x.Name.LocalName switch
        {
            "timerEventDefinition" => new TimerEventDefinition
            {
                TimeDate = x.Element("timeDate".B()) is XElement td ? new FormalExpression { Body = td.Value } : null,
                TimeDuration = x.Element("timeDuration".B()) is XElement tdu ? new FormalExpression { Body = tdu.Value } : null,
                TimeCycle = x.Element("timeCycle".B()) is XElement tc ? new FormalExpression { Body = tc.Value } : null,
            },
            "messageEventDefinition" => new MessageEventDefinition { MessageRef = x.Attr("messageRef") is string mr && idMap.TryGetValue(mr, out var me) && me is Message m ? m : null },
            "errorEventDefinition" => new ErrorEventDefinition { ErrorRef = x.Attr("errorRef") is string er && idMap.TryGetValue(er, out var ee) && ee is Error e ? e : null },
            "escalationEventDefinition" => new EscalationEventDefinition { EscalationRef = x.Attr("escalationRef") is string er && idMap.TryGetValue(er, out var es) && es is Escalation esc ? esc : null },
            "conditionalEventDefinition" => new ConditionalEventDefinition(
                x.Element("condition".B()) is XElement c ? new FormalExpression { Body = c.Value } : null!
            ),
            "linkEventDefinition" => new LinkEventDefinition(
                x.Attr("name") ?? string.Empty,
                [],
                null
            ),
            "signalEventDefinition" => new SignalEventDefinition { SignalRef = x.Attr("signalRef") is string sr && idMap.TryGetValue(sr, out var s) && s is Signal sig ? sig : null },
            "cancelEventDefinition" => new CancelEventDefinition(),
            "compensateEventDefinition" => new CompensationEventDefinition { ActivityRef = x.Attr("activityRef") is string ar && idMap.TryGetValue(ar, out var a) && a is Activity act ? act : null },
            "terminateEventDefinition" => new TerminateEventDefinition(),
            _ => null
        };

    public static BpmnModel ToModel(Bpmn.Infrastructure.Definitions definitions)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));

        var model = new BpmnModel
        {
            ProcessId = definitions.Id ?? string.Empty,
            Name = string.Empty,
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
            Diagnostics = new List<string>(),
            Shapes = new List<BPMNShape>(),
            Edges = new List<BPMNEdge>(),
            Participants = new List<Participant>(),
            Lanes = new List<Lane>(),
            MessageFlows = new List<MessageFlow>(),
            TextAnnotations = new List<TextAnnotation>(),
            Associations = new List<Association>(),
            ProcessVariables = new Dictionary<string, object>(),
            Activities = new List<object>(),
            Definitions = new List<Definition>(),
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
                    model.Messages.Add(message);
                    break;
                case Signal signal:
                    model.Signals.Add(signal);
                    break;
                case Error error:
                    model.Errors.Add(error);
                    break;
                case Escalation escalation:
                    model.Escalations.Add(escalation);
                    break;
                case Collaboration collaboration:
                    ProcessCollaboration(collaboration, model);
                    break;
            }
        }

        // Process Diagrams to extract shapes and edges
        foreach (var diagram in definitions.Diagrams)
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
            model.Properties.AddRange(process.Properties);
        }

        // Add process lanes
        if (process.LaneSets != null)
        {
            foreach (var laneSet in process.LaneSets)
            {
                if (laneSet.Lanes != null)
                {
                    model.Lanes.AddRange(laneSet.Lanes);
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
                    case Task task:
                        model.Tasks.Add(task);
                        model.ActivityIo.Add(task);
                        break;
                    case Event eventElement:
                        model.Events.Add(eventElement);
                        break;
                    case Gateway gateway:
                        model.Gateways.Add(gateway);
                        break;
                    case SequenceFlow sequenceFlow:
                        model.SequenceFlows.Add(sequenceFlow);
                        break;
                    case SubProcess subProcess:
                        model.Subprocesses.Add(subProcess);
                        model.ActivityIo.Add(subProcess);
                        break;
                    case DataObject dataObject:
                        model.DataObjects.Add(dataObject);
                        break;
                    case DataObjectReference dataObjectRef:
                        model.DataObjectReferences.Add(dataObjectRef);
                        break;
                    case DataStoreReference dataStoreRef:
                        model.DataStoreReferences.Add(dataStoreRef);
                        break;
                    case TextAnnotation textAnnotation:
                        model.TextAnnotations.Add(textAnnotation);
                        break;
                    case Association association:
                        model.Associations.Add(association);
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
                        model.TextAnnotations.Add(textAnnotation);
                        break;
                    case Association association:
                        model.Associations.Add(association);
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
            model.Participants.AddRange(collaboration.Participants);
        }

        // Add message flows
        if (collaboration.MessageFlows != null)
        {
            model.MessageFlows.AddRange(collaboration.MessageFlows);
        }

        // Add conversations (if any artifacts)
        if (collaboration.Artifacts != null)
        {
            foreach (var artifact in collaboration.Artifacts)
            {
                switch (artifact)
                {
                    case TextAnnotation textAnnotation:
                        model.TextAnnotations.Add(textAnnotation);
                        break;
                    case Association association:
                        model.Associations.Add(association);
                        break;
                }
            }
        }
    }

    private static void ProcessDiagram(BPMNDiagram diagram, BpmnModel model)
    {
        if (diagram.BPMNPlane?.Shapes != null)
        {
            model.Shapes.AddRange(diagram.BPMNPlane.Shapes);
        }

        if (diagram.BPMNPlane?.Edges != null)
        {
            model.Edges.AddRange(diagram.BPMNPlane.Edges);
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