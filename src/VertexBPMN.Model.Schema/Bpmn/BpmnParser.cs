using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn;


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
        set.XmlResolver = null; // Prevent external resolution
        set.Add("http://www.omg.org/spec/BPMN/20100524/MODEL", "Schemas/BPMN20/BPMN20.xsd");
        set.Add("http://www.omg.org/spec/BPMN/20100524/DI", "Schemas/BPMN20/BPMNDI.xsd");
        set.Add("http://www.omg.org/spec/DD/20100524/DC", "Schemas/BPMN20/DC.xsd");
        set.Add("http://www.omg.org/spec/DD/20100524/DI", "Schemas/BPMN20/DI.xsd");
        set.Add("http://www.omg.org/spec/BPMN/20100524/MODEL", "Schemas/BPMN20/Semantic.xsd");

        set.CompilationSettings = new XmlSchemaCompilationSettings { EnableUpaCheck = true };
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

    private void ValidateXmlAgainstSchemas2(string xml, List<string> diagnostics)
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
            // Log using the instance logger
            _logger.LogError("{Severity}: {Message}{Location}", sev, args.Message, loc);
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
           foreach (var error in result.Errors)
           {
               diagnostics.Add(error);
           }
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
            // Log using the instance logger
            _logger.LogError("{Severity}: {Message}{Location}", sev, args.Message, loc);
        };

        // Data structures for logical (post-schema) checks
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        var candidateRefs = new List<(string Raw, int? Line, int? Col, string? Prefix)>();
        // Expanded attribute names that hold references (best-effort list, extended)
        var refAttributeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "sourceRef","targetRef","default","defaultFlow","messageRef","signalRef","errorRef",
        "escalationRef","itemRef","operationRef","processRef","calledElementRef","calledElement",
        "dataStoreRef","dataInputRef","dataOutputRef","loopDataInputRef","loopDataOutputRef",
        "inputRef","outputRef","activityRef","bpmnElement","categoryValueRef","interfaceRef",
        "endPointRef","compensationRef","attachedToRef", "resourceRef", "formalExpressionRef" // Ergänzungen
    };

        try
        {
            using var sr = new StringReader(xml);
            using var reader = XmlReader.Create(sr, settings);
            var lineInfo = reader as IXmlLineInfo;
            var nsManager = new XmlNamespaceManager(reader.NameTable); // Für NS-Awareness

            int elementCount = 0;
            int depth = 0;
            int maxDepth = 0;
            const int MaxAllowedDepth = 1000; // Tunable Limit

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    elementCount++;
                    if (!reader.IsEmptyElement)
                    {
                        depth++;
                        if (depth > maxDepth) maxDepth = depth;
                        if (depth > MaxAllowedDepth)
                            diagnostics.Add($"Warning: Nesting depth exceeds safe limit {MaxAllowedDepth} (potential recursion at depth {depth}).");
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

            // Post-pass: attempt to resolve references (with NS)
            var unresolved = new List<(string Raw, int? Line, int? Col)>();
            foreach (var c in candidateRefs)
            {
                // NS-Resolution: Lookup prefix if available
                var nsUri = c.Prefix != null ? nsManager.LookupNamespace(c.Prefix) : null;
                if (ResolveReference(c.Raw, ids, nsUri)) continue;
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

        static bool ResolveReference(string raw, HashSet<string> ids, string? nsUri = null)
        {
            if (ids.Contains(raw)) return true;

            var colon = raw.IndexOf(':');
            if (colon > 0 && colon < raw.Length - 1)
            {
                // Best-effort: fallback to local part
                var local = raw[(colon + 1)..];
                if (ids.Contains(local)) return true;
                // Optional: NS-Check (z. B. wenn raw ein full QName ist und nsUri matcht – erweiterbar)
                if (!string.IsNullOrEmpty(nsUri) && raw.StartsWith(nsUri + ":", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
    public static Definitions Read(XDocument doc)
    {
        var bpmn = Ns.BPMN;
        var root = doc.Root ?? throw new InvalidOperationException("Missing definitions");

        _rawSequenceFlowRefs.Clear();
        _rawBoundaryAttachRefs.Clear();

        var defs = new Definitions
        {
            Id = root.Attr("id") ?? string.Empty,
            TargetNamespace = root.Attr("targetNamespace") ?? "http://example.com"
        };

        foreach (var imp in root.Elements("import".B()))
        {
            defs.Import.Add(new Import
            {
                ImportType = imp.Attr("importType") ?? string.Empty,
                Location = imp.Attr("location") ?? string.Empty,
                Namespace = imp.Attr("namespace") ?? string.Empty
            });
        }

        var idMap = new Dictionary<string, BaseElement>(StringComparer.Ordinal);

        foreach (var el in root.Elements())
        {
            if (el.Name.Namespace != bpmn) continue;
            var id = el.Attr("id");
            switch (el.Name.LocalName)
            {
                case "itemDefinition":
                    var structureRefStr = el.Attr("structureRef");
                    var idef1 = new ItemDefinition
                    {
                        Id = id ?? string.Empty,
                        StructureRef = structureRefStr is not null ? new XmlQualifiedName(structureRefStr) : XmlQualifiedName.Empty,
                        IsCollection = el.Attr("isCollection") is string ic && bool.TryParse(ic, out var bc) && bc
                    };
                    defs.RootElement.Add(idef1);
                    idMap[idef1.Id] = idef1;
                    break;

                case "message":
                    var msg = new Message { Id = id ?? string.Empty, Name = el.Attr("name") ?? string.Empty };
                    defs.RootElement.Add(msg);
                    idMap[msg.Id] = msg;
                    break;

                case "resource":
                    var res = new Resource
                    {
                        Id = id ?? string.Empty,
                        Name = el.Attr("name") ?? string.Empty
                    };
                    defs.RootElement.Add(res);
                    idMap[res.Id] = res;
                    break;

                case "category":
                    var cat = new Category { Id = id ?? string.Empty, Name = el.Attr("name") ?? string.Empty };
                    foreach (var cv in el.Elements("categoryValue".B()))
                    {
                        var cvId = cv.Attr("id") ?? string.Empty;
                        var v = new CategoryValue { Id = cvId, Value = cv.Attr("value") ?? string.Empty };
                        cat.CategoryValue.Add(v);
                        idMap[v.Id] = v;
                    }
                    defs.RootElement.Add(cat);
                    idMap[cat.Id] = cat;
                    break;

                case "error":
                    var err = new Error
                    {
                        Id = id ?? string.Empty,
                        Name = el.Attr("name") ?? string.Empty,
                        ErrorCode = el.Attr("errorCode") ?? string.Empty,
                        StructureRef = el.Attr("structureRef") is string stref ? new XmlQualifiedName(stref) : XmlQualifiedName.Empty
                    };
                    defs.RootElement.Add(err);
                    idMap[err.Id] = err;
                    break;

                case "escalation":
                    var esc = new Escalation
                    {
                        Id = id ?? string.Empty,
                        Name = el.Attr("name") ?? string.Empty,
                        EscalationCode = el.Attr("escalationCode") ?? string.Empty,
                        StructureRef = el.Attr("structureRef") is string es ? new XmlQualifiedName(es) : XmlQualifiedName.Empty
                    };
                    defs.RootElement.Add(esc);
                    idMap[esc.Id] = esc;
                    break;

                case "interface":
                    var iface = new Interface
                    {
                        Id = id ?? string.Empty,
                        Name = el.Attr("name") ?? string.Empty,
                        ImplementationRef = el.Attr("implementationRef") is string sref ? new XmlQualifiedName(sref) : XmlQualifiedName.Empty
                    };
                    foreach (var opEl in el.Elements("operation".B()))
                    {
                        var opId = opEl.Attr("id") ?? string.Empty;
                        var op = new Operation
                        {
                            Id = opId,
                            Name = opEl.Attr("name") ?? string.Empty,
                            InMessageRef = XmlQualifiedName.Empty,
                            OutMessageRef = XmlQualifiedName.Empty,
                            ImplementationRef = XmlQualifiedName.Empty
                        };
                        iface.Operation.Add(op);
                    }
                    defs.RootElement.Add(iface);
                    idMap[iface.Id] = iface;
                    break;

                case "signal":
                    var s = new Signal { Id = id ?? string.Empty, Name = el.Attr("name") ?? string.Empty };
                    defs.RootElement.Add(s);
                    idMap[s.Id] = s;
                    break;

                case "process":
                    var p = new Process
                    {
                        Id = id ?? string.Empty,
                        Name = el.Attr("name") ?? string.Empty,
                        IsExecutable = el.AttrBool("isExecutable") ?? false
                    };
                    defs.RootElement.Add(p);
                    idMap[p.Id] = p;
                    break;

                case "collaboration":
                    var c = new Collaboration { Id = id ?? string.Empty };
                    defs.RootElement.Add(c);
                    idMap[c.Id] = c;
                    break;

                case "choreography":
                    var ch = new Choreography { Id = id ?? string.Empty };
                    defs.RootElement.Add(ch);
                    idMap[ch.Id] = ch;
                    break;

                case "relationship":
                    var rel = new Relationship
                    {
                        Id = id ?? string.Empty,
                        Type = el.Attr("type") ?? string.Empty,
                        Direction = RelationshipDirection.None
                    };
                    defs.Relationship.Add(rel);
                    idMap[rel.Id] = rel;
                    break;

                case "BPMNDiagram":
                case "BPMNPlane":
                case "BPMNShape":
                case "BPMNEdge":
                    break;
            }
        }

        foreach (var el in root.Elements())
        {
            switch (el.Name.LocalName)
            {
                case "message":
                    {
                        var mid = el.Attr("id");
                        if (mid is null || !idMap.TryGetValue(mid, out var obj) || obj is not Message msg) break;
                        var iref = el.Attr("itemRef");
                        if (iref is not null)
                        {
                            msg.ItemRef = new XmlQualifiedName(iref);
                            idMap[msg.Id] = msg;
                        }
                        break;
                    }

                case "interface":
                    {
                        var iid = el.Attr("id");
                        if (iid is null || !idMap.TryGetValue(iid, out var obj) || obj is not Interface iface) break;
                        foreach (var opEl in el.Elements("operation".B()))
                        {
                            var opId = opEl.Attr("id");
                            if (opId is null) continue;
                            var op = iface.Operation.FirstOrDefault(x => x.Id == opId);
                            if (op is null) continue;

                            var inMsgAttr = opEl.Attr("inMessageRef");
                            if (inMsgAttr is string inRef && !string.IsNullOrWhiteSpace(inRef))
                                op.InMessageRef = new XmlQualifiedName(inRef);

                            var outMsgAttr = opEl.Attr("outMessageRef");
                            if (outMsgAttr is string outRef && !string.IsNullOrWhiteSpace(outRef))
                                op.OutMessageRef = new XmlQualifiedName(outRef);

                            var implAttr = opEl.Attr("implementationRef");
                            op.ImplementationRef = implAttr is string impl && !string.IsNullOrWhiteSpace(impl)
                                ? new XmlQualifiedName(impl)
                                : XmlQualifiedName.Empty;

                            foreach (var erEl in opEl.Elements("errorRef".B()))
                            {
                                var rawId = erEl.Value?.Trim();
                                if (string.IsNullOrEmpty(rawId)) continue;
                                op.ErrorRef.Add(new XmlQualifiedName(rawId));
                            }
                        }
                        break;
                    }

                case "process":
                    {
                        var pid = el.Attr("id");
                        if (pid is null || !idMap.TryGetValue(pid, out var obj) || obj is not Process p) break;

                        foreach (var child in el.Elements())
                        {
                            if (child.Name == "ioSpecification".B())
                            {
                                p.IoSpecification = ReadIOSpec(child);
                                continue;
                            }

                            if (child.Name == "laneSet".B())
                            {
                                var ls = new LaneSet { Id = child.Attr("id") ?? string.Empty, Name = child.Attr("name") ?? string.Empty };
                                foreach (var ln in child.Elements("lane".B()))
                                {
                                    var lane = new Lane
                                    {
                                        Id = ln.Attr("id") ?? string.Empty,
                                        Name = ln.Attr("name") ?? string.Empty
                                    };
                                    foreach (var fnr in ln.Elements("flowNodeRef".B()))
                                    {
                                        var refId = fnr.Value?.Trim();
                                        if (!string.IsNullOrEmpty(refId))
                                            lane.FlowNodeRef.Add(refId);
                                    }
                                    ls.Lane.Add(lane);
                                }
                                p.LaneSet.Add(ls);
                                continue;
                            }
                            if (child.Name.LocalName == "textAnnotation")
                            {
                                var ta = new TextAnnotation
                                {
                                    Id = child.Attr("id") ?? string.Empty,
                                    Text = child.Element("text".B()) is XElement textEl
                                        ? new TText { Text = new[] { textEl.Value } }
                                        : null,
                                    TextFormat = child.Attr("textFormat") ?? "text/plain"
                                };
                                p.Artifact.Add(ta);
                                idMap[ta.Id] = ta;
                                continue;
                            }
                            if (child.Name.LocalName == "association")
                            {
                                var assoc = new Association
                                {
                                    Id = child.Attr("id") ?? string.Empty,
                                    AssociationDirection = AssociationDirection.None,
                                    SourceRef = child.Attr("sourceRef") is string sId && idMap.TryGetValue(sId, out var sEl)
                                        ? new XmlQualifiedName(sEl.Id)
                                        : XmlQualifiedName.Empty,
                                    TargetRef = child.Attr("targetRef") is string tId && idMap.TryGetValue(tId, out var tEl)
                                        ? new XmlQualifiedName(tEl.Id)
                                        : XmlQualifiedName.Empty
                                };
                                p.Artifact.Add(assoc);
                                idMap[assoc.Id] = assoc;
                                continue;
                            }
                            if (child.Name.LocalName == "group")
                            {
                                var group = new Group
                                {
                                    Id = child.Attr("id") ?? string.Empty
                                };
                                var catRef = child.Attr("categoryValueRef");
                                if (!string.IsNullOrWhiteSpace(catRef))
                                    group.CategoryValueRef = new XmlQualifiedName(catRef);
                                p.Artifact.Add(group);
                                idMap[group.Id] = group;
                                continue;
                            }
                            // Flow elements
                            var fe = ReadFlowElement(child, idMap);
                            if (fe != null)
                                p.FlowElement.Add(fe);

                            if (child.Name.LocalName == "dataObject" && child.Name.Namespace == bpmn)
                            {
                                var dobj = new DataObject
                                {
                                    Id = child.Attr("id") ?? string.Empty,
                                    Name = child.Attr("name") ?? string.Empty,
                                    IsCollection = child.AttrBool("isCollection") == true
                                };
                                idMap[dobj.Id] = dobj;
                                p.FlowElement.Add(dobj);
                            }
                            else if (child.Name.LocalName == "dataObjectReference" && child.Name.Namespace == bpmn)
                            {
                                var refId = child.Attr("dataObjectRef");
                                if (!string.IsNullOrWhiteSpace(refId))
                                {
                                    var dref = new DataObjectReference
                                    {
                                        Id = child.Attr("id") ?? string.Empty,
                                        DataObjectRef = refId
                                    };
                                    idMap[dref.Id] = dref;
                                    p.FlowElement.Add(dref);
                                }
                            }
                            else if (child.Name.LocalName == "dataStore" && child.Name.Namespace == bpmn)
                            {
                                var ds = new DataStore
                                {
                                    Id = child.Attr("id") ?? string.Empty,
                                    Name = child.Attr("name") ?? string.Empty
                                };
                                idMap[ds.Id] = ds;
                            }
                            else if (child.Name.LocalName == "dataStoreReference" && child.Name.Namespace == bpmn)
                            {
                                var refId = child.Attr("dataStoreRef");
                                if (!string.IsNullOrWhiteSpace(refId))
                                {
                                    var dsr = new DataStoreReference
                                    {
                                        Id = child.Attr("id") ?? string.Empty,
                                        DataStoreRef = new XmlQualifiedName(refId)
                                    };
                                    idMap[dsr.Id] = dsr;
                                    p.FlowElement.Add(dsr);
                                }
                            }

                            // Data associations for activities
                            if (fe is Activity act)
                            {
                                var inAssocs = new List<DataInputAssociation>();
                                foreach (var dia in child.Elements("dataInputAssociation".B()))
                                {
                                    var sources = new List<string>();
                                    foreach (var sr in dia.Elements("sourceRef".B()))
                                    {
                                        var sid = sr.Value?.Trim();
                                        if (!string.IsNullOrEmpty(sid))
                                            sources.Add(sid);
                                    }
                                    var targId = dia.Element("targetRef".B())?.Value?.Trim();
                                    if (!string.IsNullOrEmpty(targId))
                                    {
                                        var assoc = new DataInputAssociation
                                        {
                                            SourceRef = sources,
                                            TargetRef = targId
                                        };
                                        inAssocs.Add(assoc);
                                    }
                                }

                                var outAssocs = new List<DataOutputAssociation>();
                                foreach (var doa in child.Elements("dataOutputAssociation".B()))
                                {
                                    var sources = new List<string>();
                                    foreach (var sr in doa.Elements("sourceRef".B()))
                                    {
                                        var sid = sr.Value?.Trim();
                                        if (!string.IsNullOrEmpty(sid))
                                            sources.Add(sid);
                                    }
                                    var targId = doa.Element("targetRef".B())?.Value?.Trim();
                                    if (!string.IsNullOrEmpty(targId))
                                    {
                                        var assoc = new DataOutputAssociation
                                        {
                                            SourceRef = sources,
                                            TargetRef = targId
                                        };
                                        outAssocs.Add(assoc);
                                    }
                                }

                                if (inAssocs.Count > 0)
                                {
                                    foreach (var a in inAssocs)
                                        act.DataInputAssociation.Add(a);
                                }
                                if (outAssocs.Count > 0)
                                {
                                    foreach (var a in outAssocs)
                                        act.DataOutputAssociation.Add(a);
                                }
                            }
                        }
                        break;
                    }

                case "collaboration":
                    {
                        var cid = el.Attr("id");
                        if (cid is null || !idMap.TryGetValue(cid, out var obj) || obj is not Collaboration c) break;

                        foreach (var child in el.Elements())
                        {
                            if (child.Name.LocalName == "messageFlow")
                            {
                                var mf = new MessageFlow
                                {
                                    Id = child.Attr("id") ?? string.Empty,
                                    SourceRef = new XmlQualifiedName(child.Attr("sourceRef") ?? string.Empty),
                                    TargetRef = new XmlQualifiedName(child.Attr("targetRef") ?? string.Empty)
                                };
                                var mref = child.Attr("messageRef");
                                if (!string.IsNullOrWhiteSpace(mref))
                                    mf.MessageRef = new XmlQualifiedName(mref);
                                c.MessageFlow.Add(mf);
                                idMap[mf.Id] = mf;
                            }
                        }
                        break;
                    }
            }
        }

        foreach (var xdiag in root.Elements("BPMNDiagram".BPMNDI()))
        {
            var planeEl = xdiag.Element("BPMNPlane".BPMNDI());
            if (planeEl is null) continue;

            var plane = new BpmnPlane
            {
                Id = planeEl.Attr("id") ?? string.Empty,
                BpmnElement = planeEl.Attr("bpmnElement") is string pe ? new XmlQualifiedName(pe) : XmlQualifiedName.Empty
            };

            foreach (var s in planeEl.Elements("BPMNShape".BPMNDI()))
            {
                var shape = new BpmnShape
                {
                    Id = s.Attr("id") ?? string.Empty,
                    BpmnElement = s.Attr("bpmnElement") is string sb ? new XmlQualifiedName(sb) : XmlQualifiedName.Empty
                };
                plane.DiagramElement.Add(shape);
            }

            foreach (var e in planeEl.Elements("BPMNEdge".BPMNDI()))
            {
                var edge = new BpmnEdge
                {
                    Id = e.Attr("id") ?? string.Empty,
                    BpmnElement = e.Attr("bpmnElement") is string eb ? new XmlQualifiedName(eb) : XmlQualifiedName.Empty
                };
                plane.DiagramElement.Add(edge);
            }

            var diagram = new BpmnDiagram
            {
                Id = xdiag.Attr("id") ?? string.Empty,
                BpmnPlane = plane
            };
            defs.BpmnDiagram.Add(diagram);
        }

        ResolveForwardReferences(defs, idMap);
        return defs;
    }

    private static void ResolveForwardReferences(Definitions defs, Dictionary<string, BaseElement> idMap)
    {
        foreach (var proc in defs.RootElement.OfType<Process>())
        {
            for (int i = 0; i < proc.FlowElement.Count; i++)
            {
                switch (proc.FlowElement[i])
                {
                    case SequenceFlow sf when sf.Id is not null && _rawSequenceFlowRefs.TryGetValue(sf.Id, out var raw):
                        var updated = sf;
                        if (sf.SourceRef == null && raw.SourceRef is string sId && idMap.TryGetValue(sId, out var sEl) && sEl is FlowNode sNode)
                            updated.SourceRef = sNode.Id; // FIX: assign Id (string) instead of FlowNode
                        if (sf.TargetRef == null && raw.TargetRef is string tId && idMap.TryGetValue(tId, out var tEl) && tEl is FlowNode tNode)
                            updated.TargetRef = tNode.Id; // FIX: assign Id (string) instead of FlowNode
                        if (!ReferenceEquals(updated, sf))
                        {
                            proc.FlowElement[i] = updated;
                            idMap[sf.Id] = updated;
                        }
                        break;
                    case BoundaryEvent be when be.Id is not null && be.AttachedToRef == null && _rawBoundaryAttachRefs.TryGetValue(be.Id, out var attId) && attId is not null:
                        if (idMap.TryGetValue(attId, out var aEl) && aEl is Activity act)
                        {
                            be.AttachedToRef = new XmlQualifiedName(act.Id ?? string.Empty);
                            proc.FlowElement[i] = be;
                            idMap[be.Id] = be;
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
            io.DataInput.Add(d); if (d.Id is not null) inMap[d.Id] = d;
        }
        foreach (var @do in x.Elements("dataOutput".B()))
        {
            var d = new DataOutput { Id = @do.Attr("id"), Name = @do.Attr("name") };
            io.DataOutput.Add(d); if (d.Id is not null) outMap[d.Id] = d;
        }

        foreach (var set in x.Elements("inputSet".B()))
        {
            var s = new InputSet { Id = set.Attr("id") };
            foreach (var r in set.Elements("dataInputRef".B()))
                if ((string?)r is string id) s.DataInputRefs.Add(id);
            io.InputSet.Add(s);
        }
        foreach (var set in x.Elements("outputSet".B()))
        {
            var s = new OutputSet { Id = set.Attr("id") };
            foreach (var r in set.Elements("dataOutputRef".B()))
                if ((string?)r is string id) s.DataOutputRefs.Add(id);
            io.OutputSet.Add(s);
        }
        return io;
    }

    static FlowElement? ReadFlowElement(XElement x, Dictionary<string, BaseElement> idMap)
    {
        FlowElement? fe = x.Name.LocalName switch
        {
            // Activities / Tasks
            "task" => new Task
            {
                Id = x.Attr("id"),
                Name = x.Attr("name"),
                AnyAttribute = CreateAnyAttributes(x)
            },
            // ServiceTask: correct BPMN attribute is 'implementation' (lowercase)
            "serviceTask" => new ServiceTask { Id = x.Attr("id"), Name = x.Attr("name"), Implementation = x.Attr("implementation") },
            "userTask" => new UserTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "scriptTask" => new ScriptTask()
            {
                Id = x.Attr("id"),
                Name = x.Attr("name"),
                ScriptFormat = x.Attr("scriptFormat") ?? "",
                Script = x.Element("script".B()) is XElement scriptEl
                    ? new Script { Text = new[] { scriptEl.Value } }
                    : null
            },
            "manualTask" => new ManualTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "businessRuleTask" => new BusinessRuleTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "sendTask" => new SendTask { Id = x.Attr("id"), Name = x.Attr("name") },
            "receiveTask" => new ReceiveTask { Id = x.Attr("id"), Name = x.Attr("name"), Implementation = x.Attr("implementation"), Instantiate = x.AttrBool("instantiate") ?? false },
            "callActivity" => new CallActivity
            {
                Id = x.Attr("id"),
                // CalledElement should be XmlQualifiedName, not CallableElement.
                // Use the attribute value directly, or XmlQualifiedName.Empty if missing.
                CalledElement = x.Attr("calledElementRef") is string cref && !string.IsNullOrWhiteSpace(cref)
                    ? new XmlQualifiedName(cref)
                    : XmlQualifiedName.Empty
            },

            // SubProcess / Transaction / AdHoc
            "subProcess" => new SubProcess
            {
                Id = x.Attr("id") ?? string.Empty,
                TriggeredByEvent = x.AttrBool("triggeredByEvent") ?? false
            },
            "transaction" => new Transaction { Id = x.Attr("id") ?? string.Empty, Method = x.Attr("method") ?? string.Empty },
            "adHocSubProcess" => new AdHocSubProcess { Id = x.Attr("id") ?? string.Empty },

            // Gateways
            "exclusiveGateway" => new ExclusiveGateway { Id = x.Attr("id") ?? string.Empty, Name = x.Attr("name") ?? string.Empty },
            "inclusiveGateway" => new InclusiveGateway { Id = x.Attr("id") ?? string.Empty, Name = x.Attr("name") ?? string.Empty },
            "parallelGateway" => new ParallelGateway { Id = x.Attr("id") ?? string.Empty, Name = x.Attr("name") ?? string.Empty },
            "complexGateway" => new ComplexGateway { Id = x.Attr("id") ?? string.Empty, Name = x.Attr("name") ?? string.Empty },
            "eventBasedGateway" => new EventBasedGateway
            {
                Id = x.Attr("id") ?? string.Empty,
                Name = x.Attr("name") ?? string.Empty,
                Instantiate = x.AttrBool("instantiate") ?? false
            },

            // Events
            "startEvent" => new StartEvent
            {
                Id = x.Attr("id") ?? string.Empty,
                Name = x.Attr("name") ?? "startEvent",
                IsInterrupting = x.AttrBool("isInterrupting") ?? false
            },
            "endEvent" => new EndEvent { Id = x.Attr("id") ?? string.Empty, Name = x.Attr("name") ?? "endEvent" },
            "intermediateCatchEvent" => new IntermediateCatchEvent { Id = x.Attr("id") ?? string.Empty },
            "intermediateThrowEvent" => new IntermediateThrowEvent { Id = x.Attr("id") ?? string.Empty },
            "boundaryEvent" => new BoundaryEvent
            {
                Id = x.Attr("id") ?? string.Empty,
                Name = x.Attr("name") ?? string.Empty,
                CancelActivity = x.AttrBool("cancelActivity") ?? false,
                AttachedToRef = x.Attr("attachedToRef") is string att && !string.IsNullOrWhiteSpace(att)
                    ? new XmlQualifiedName(att)
                    : XmlQualifiedName.Empty
            },

            // Sequence Flow
            "sequenceFlow" => new SequenceFlow
            {
                Id = x.Attr("id") ?? string.Empty,
                Name = x.Attr("name") ?? string.Empty,
                SourceRef = x.Attr("sourceRef") ?? string.Empty,
                TargetRef = x.Attr("targetRef") ?? string.Empty
            },


            _ => null
        };

       
        if (fe is null) return null;
        if (fe.Id is not null) idMap[fe.Id] = fe;

        // child content / refs
        switch (fe)
        {
            case StartEvent se:
                foreach (var ed in x.Elements()) se.EventDefinition.Add(ReadEventDefinition(ed, idMap));
                break;
            case EndEvent ee:
                foreach (var ed in x.Elements()) ee.EventDefinition.Add(ReadEventDefinition(ed, idMap));
                break;
            case IntermediateCatchEvent ice:
                foreach (var ed in x.Elements()) ice.EventDefinition.Add(ReadEventDefinition(ed, idMap));
                break;
            case IntermediateThrowEvent ite:
                foreach (var ed in x.Elements()) ite.EventDefinition.Add(ReadEventDefinition(ed, idMap));
                break;
            case BoundaryEvent be:
                var att = x.Attr("attachedToRef");
                if (be.AttachedToRef == null && att is not null)
                {
                    // store raw for later resolution
                    if (be.Id is not null) _rawBoundaryAttachRefs[be.Id] = att;
                }
                if (att is not null && idMap.TryGetValue(att, out var bae) && bae is FlowElement fo && fo is Activity act)
                    be.AttachedToRef = new XmlQualifiedName(act.Name);
                foreach (var ed in x.Elements()) be.EventDefinition.Add(ReadEventDefinition(ed, idMap));
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
                                LoopCardinality= new FormalExpression { Text = [loopCard] },
                                IsSequential= isSequential,
                                Behavior=  MultiInstanceFlowCondition.All,
                                CompletionCondition= string.IsNullOrWhiteSpace(completionCondition) ? null : new FormalExpression { Text = [completionCondition] }
                            };
                            sp.LoopCharacteristics = miLoop ;
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
                            sp.LoopCharacteristics = stdLoop;
                        }
                    }
                    // Data associations for activities inside subprocess
                    if (child.Name.LocalName == "dataInputAssociation" || child.Name.LocalName == "dataOutputAssociation")
                    {
                        // handled below after activity creation
                    }

                    var cfe = ReadFlowElement(child, idMap);
                    if (cfe is not null) sp.FlowElement.Add(cfe);
                    if (cfe is Activity activity)
                    {
                        var inAssocs = new List<DataInputAssociation>();
                        foreach (var dia in child.Elements("dataInputAssociation".B()))
                        {
                            var sources = new List<string>();
                            foreach (var sr in dia.Elements("sourceRef".B()))
                                if ((string?)sr is string sid && idMap.TryGetValue(sid, out var sEl)) sources.Add(sEl.Id);
                            string? target = null;
                            var targId = dia.Element("targetRef".B())?.Value;
                            if (targId is not null && idMap.TryGetValue(targId, out var tEl))
                                target = tEl.Id;
                            if (target is not null)
                                inAssocs.Add(new DataInputAssociation { SourceRef = sources, TargetRef = target });
                        }
                        var outAssocs = new List<DataOutputAssociation>();
                        foreach (var doa in child.Elements("dataOutputAssociation".B()))
                        {
                            var sources = new List<string>();
                            foreach (var sr in doa.Elements("sourceRef".B()))
                                if ((string?)sr is string sid && idMap.TryGetValue(sid, out var sEl)) sources.Add(sEl.Id);
                            string? target = null;
                            var targId = doa.Element("targetRef".B())?.Value;
                            if (targId is not null && idMap.TryGetValue(targId, out var tEl)) target = tEl.Id;
                            if (target is not null)
                                outAssocs.Add(new DataOutputAssociation { SourceRef = sources, TargetRef = target });
                        }
                        if (inAssocs.Count > 0 || outAssocs.Count > 0)
                        {
                            activity.DataInputAssociation = inAssocs;
                            activity.DataOutputAssociation = outAssocs;
                            sp.FlowElement[sp.FlowElement.Count - 1] = activity;
                            if (activity.Id is not null) idMap[activity.Id] = activity;
                        }
                    }
                }
                break;
            case SequenceFlow sf:
                var rawSource = x.Attr("sourceRef");
                var rawTarget = x.Attr("targetRef");
                if (sf.Id is not null) _rawSequenceFlowRefs[sf.Id] = (rawSource, rawTarget);
                if (rawSource is string sref && idMap.TryGetValue(sref, out var s) && s is FlowNode sn)
                    sf.SourceRef = sn.Name;
                if (rawTarget is string tref && idMap.TryGetValue(tref, out var t) && t is FlowNode tn)
                    sf.TargetRef = tn.Name;
                var cond = x.Element("conditionExpression".B());
                if (cond is not null)
                    sf.ConditionExpression = new FormalExpression { Text = [cond.Value]};
                break;
        }

        // Parse <incoming> / <outgoing> textual references for FlowNodes (tasks, events, gateways)
        if (fe is FlowNode fn)
        {
            var incomingIds = x.Elements("incoming".B()).Select(e => e.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var outgoingIds = x.Elements("outgoing".B()).Select(e => e.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (incomingIds.Count > 0 || outgoingIds.Count > 0)
            {
                var inFlows = new List<SequenceFlow>();
                foreach (var id in incomingIds)
                    if (idMap.TryGetValue(id, out var sfEl) && sfEl is SequenceFlow sfi) inFlows.Add(sfi);
                var outFlows = new List<SequenceFlow>();
                foreach (var id in outgoingIds)
                    if (idMap.TryGetValue(id, out var sfEl) && sfEl is SequenceFlow sfo) outFlows.Add(sfo);
                inFlows.ForEach( i => fn.Incoming.Add(new XmlQualifiedName(i.Name)));
                outFlows.ForEach( o => fn.Outgoing.Add(new XmlQualifiedName(o.Name)));
                fe = fn;
                if (fe.Id is not null) idMap[fe.Id] = fe; // refresh mapping with updated collections
            }
        }

        return fe;
    }

    private static List<XmlAttribute> CreateAnyAttributes(XElement x)
    {
        var attributes = new List<XmlAttribute>();
        var doc = new XmlDocument();

        void AddAttr(string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var attr = doc.CreateAttribute(name);
                attr.Value = value;
                attributes.Add(attr);
            }
        }

        AddAttr("isForCompensation", x.Attr("isForCompensation"));
        AddAttr("startQuantity", x.Attr("startQuantity"));
        AddAttr("completionQuantity", x.Attr("completionQuantity"));

        return attributes;
    }

    static EventDefinition ReadEventDefinition(XElement x, Dictionary<string, BaseElement> idMap)
        => x.Name.LocalName switch
        {
            "timerEventDefinition" => new TimerEventDefinition
            {
                TimeDate = x.Element("timeDate".B()) is XElement td ? new FormalExpression { Text = [td.Value]} : null,
                TimeDuration = x.Element("timeDuration".B()) is XElement tdu ? new FormalExpression { Text = [tdu.Value] } : null,
                TimeCycle = x.Element("timeCycle".B()) is XElement tc ? new FormalExpression { Text = [tc.Value] } : null,
            },
            "messageEventDefinition" => new MessageEventDefinition
            {
                MessageRef = x.Attr("messageRef") is string mr && !string.IsNullOrWhiteSpace(mr)
                    ? new XmlQualifiedName(mr)
                    : XmlQualifiedName.Empty
            },
            "errorEventDefinition" => new ErrorEventDefinition {
                ErrorRef = x.Attr("errorRef") is string er && !string.IsNullOrWhiteSpace(er)
                    ? new XmlQualifiedName(er)
                    : XmlQualifiedName.Empty
            },
            "escalationEventDefinition" => new EscalationEventDefinition { 
                EscalationRef = x.Attr("escalationRef") is string er && !string.IsNullOrWhiteSpace(er)
                    ? new XmlQualifiedName(er)
                    : XmlQualifiedName.Empty
            },
            "conditionalEventDefinition" => new ConditionalEventDefinition {
               Condition =  x.Element("condition".B()) is XElement c ? new FormalExpression { Text = [c.Value] }: null}
            ,
            "linkEventDefinition" => new LinkEventDefinition{ Name =x.Attr("name") ?? string.Empty },
            "signalEventDefinition" => new SignalEventDefinition { SignalRef = x.Attr("signalRef") is string sr && !string.IsNullOrWhiteSpace(sr) ? new XmlQualifiedName(sr) : XmlQualifiedName.Empty },
            "cancelEventDefinition" => new CancelEventDefinition(),
            "compensateEventDefinition" => new CompensateEventDefinition { ActivityRef = x.Attr("activityRef") is string ar && !string.IsNullOrWhiteSpace(ar)
                ? new XmlQualifiedName(ar)
                : XmlQualifiedName.Empty
            },
            "terminateEventDefinition" => new TerminateEventDefinition(),
            _ => null
        };

    public static BpmnModel ToModel(Definitions definitions)
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
            Shapes = new List<BpmnShape>(),
            Edges = new List<BpmnEdge>(),
            Participants = new List<Participant>(),
            Lanes = new List<Lane>(),
            MessageFlows = new List<MessageFlow>(),
            TextAnnotations = new List<TextAnnotation>(),
            Associations = new List<Association>(),
            ProcessVariables = new Dictionary<string, object>(),
            Activities = new List<Activity>(),
            Definitions = new List<Definitions>(),
            ProcessDefinitions = definitions
        };

        // Process RootElements to extract process-specific elements
        foreach (var rootElement in definitions.RootElement)
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
        foreach (var diagram in definitions.BpmnDiagram)
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
        if (process.Property != null)
        {
            model.Properties.AddRange(process.Property);
        }

        // Add process lanes
        if (process.LaneSet != null)
        {
            foreach (var laneSet in process.LaneSet)
            {
                if (laneSet.Lane != null)
                {
                    model.Lanes.AddRange(laneSet.Lane);
                }
            }
        }

        // Process flow elements
        if (process.FlowElement != null)
        {
            foreach (var flowElement in process.FlowElement)
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
                    
                }
            }
        }

        // Process artifacts
        if (process.Artifact != null)
        {
            foreach (var artifact in process.Artifact)
            {
                switch (artifact)
                {
                    case TextAnnotation textAnnotation:
                        model.TextAnnotations.Add(textAnnotation);
                        break;
                    case Association association:
                        model.Associations.Add(association);
                        break;
                    case Group group:
                        model.Groups.Add(group);
                        break;
                }
            }
        }
    }

    private static void ProcessCollaboration(Collaboration collaboration, BpmnModel model)
    {
        // Add participants
        if (collaboration.Participant != null)
        {
            model.Participants.AddRange(collaboration.Participant);
        }

        // Add message flows
        if (collaboration.MessageFlow != null)
        {
            model.MessageFlows.AddRange(collaboration.MessageFlow);
        }

        // Add conversations (if any artifacts)
        if (collaboration.Artifact != null)
        {
            foreach (var artifact in collaboration.Artifact)
            {
                switch (artifact)
                {
                    case TextAnnotation textAnnotation:
                        model.TextAnnotations.Add(textAnnotation);
                        break;
                    case Association association:
                        model.Associations.Add(association);
                        break;
                    case Group group:
                        model.Groups.Add(group);
                        break;
                }
            }
        }
    }

    private static void ProcessDiagram(BpmnDiagram diagram, BpmnModel model)
    {
        if (diagram.BpmnPlane?.DiagramElement != null && diagram.BpmnPlane?.DiagramElement is IEnumerable<BpmnShape> shapes)
        {
            model.Shapes.AddRange(shapes);
        }

        if (diagram.BpmnPlane?.DiagramElement != null && diagram.BpmnPlane?.DiagramElement is IEnumerable<BpmnEdge> edges)
        {
            model.Edges.AddRange(edges);
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