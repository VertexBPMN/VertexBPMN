//See docs/ROUNDTRIP_STRICT_PLAN.md
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Serialization;

public partial class BpmnSerializer
{
	private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
	private static readonly Dictionary<string, string> WellKnownPrefixes = new(StringComparer.Ordinal)
	{
		{
			BpmnXmlNamespaces.Camunda,
			"camunda"
		},
		{
			BpmnXmlNamespaces.Zeebe,
			"zeebe"
		},
		//{"http://vertexbpmn.io/schema/1.0","vertex"},
		//{"http://vertexbpmn.io/schema/1.0/bpmn","vertex"},
		{
			"https://vertexbpmn.io/schema/bpmn/1.0",
			"vertex"
		}
	};
	public BpmnRoundtripMode RoundtripMode { get; init; } = BpmnRoundtripMode.Normalized;
	/// <summary>If false (strict only), do not synthesize missing incoming/outgoing edges.</summary>
	public bool PreserveGeneratedIfMissing { get; init; }

	public string Serialize(BpmnModel model)
	{
		var strictRequested = RoundtripMode == BpmnRoundtripMode.Strict;
		var (strict, raw) = SelectRoundtripMetadata(model, strictRequested);
		if (strict && raw is { OriginalXml: { } originalXml, PartiallyDirtyElements: null } && HasCompleteRawMetadata(originalXml, raw))
		{
			return XDocument.Parse(originalXml, LoadOptions.PreserveWhitespace).ToString(SaveOptions.DisableFormatting);
		}

		// Fallback diagnostics collection (mutate list if underlying collection is mutable)
		if (strict && raw != null && model.Diagnostics is List<string> diagList && raw.RawExtensionElements == null)
		{
			diagList.Add("RT-Fallback:extensions (RawExtensionElements missing)");
		}



		var (fallbackIncoming, fallbackOutgoing) = BuildFallbackFlowReferences(model, strict, strictRequested);


		var vendorNs = CollectVendorNamespaces(model, strict);

		var definitions = CreateDefinitionsElement(strict, raw, vendorNs);
		AppendDefinitionsGlobals(model, strict, raw, definitions);

		var proc = new XElement(Bpmn + "process", new XAttribute("id", model.ProcessId));
		RestoreProcessAttributes(strict, raw, proc);

		definitions.Add(proc);
		var context = new SerializationContext(this, model, strict, raw, proc, fallbackIncoming, fallbackOutgoing);
















		context.AppendFlowNodes();
		AppendRemainingModelContent(model, strict, raw, fallbackIncoming, fallbackOutgoing, proc, definitions);
		var doc = new XDocument(definitions);
		AppendMissingCollaboration(model, strictRequested, strict, definitions);

		return doc.ToString(SaveOptions.DisableFormatting);
	}

	private static XElement CreateDefinitionsElement(bool strict, BpmnRawMetadata? raw, HashSet<string> vendorNs)
	{
		var definitions = new XElement(Bpmn + "definitions");
		RestoreNamespacePrefixOrder(strict, raw, definitions);

		RestoreExtensionNamespaceDeclarations(strict, raw, definitions);

		RestoreDefinitionsAttributes(strict, raw, vendorNs, definitions);

		return definitions;
	}

	private static (bool Strict, BpmnRawMetadata? Raw) SelectRoundtripMetadata(BpmnModel model, bool strictRequested)
	{
		var strict = strictRequested && model.RawMetadata?.RoundtripDirty == false;
		if (strictRequested && model.RawMetadata?.RoundtripDirty == true && model.Diagnostics is List<string> dl && !dl.Contains("RT-Fallback:dirty-roundtrip"))
		{
			dl.Add("RT-Fallback:dirty-roundtrip");
		}



		var raw = strict ? model.RawMetadata : null;
		return (strict, raw);
	}

	private void AppendRemainingModelContent(
		BpmnModel model, bool strict, BpmnRawMetadata? raw,
		Dictionary<string, List<string>>? fallbackIncoming,
		Dictionary<string, List<string>>? fallbackOutgoing,
		XElement proc, XElement definitions)
	{
		NestNormalizedSubprocessNodes(model, strict, proc);

		if (strict && raw?.RawArtifacts is { Count: > 0 })
		{
			foreach (var art in raw.RawArtifacts)
			{
				proc.Add(new XElement(art));
			}
		}

		RestoreLaneSets(strict, raw, proc);

		FillMissingStrictFlowReferences(strict, fallbackIncoming, fallbackOutgoing, proc);

		AppendNormalizedDataObjects(model, strict, proc);

		AppendNormalizedActivityIo(model, strict, proc);

		AppendNormalizedDiagram(model, strict, raw, definitions);

	}

	private static bool HasCompleteRawMetadata(string originalXml, BpmnRawMetadata raw)
	{
		var hasExtensions = originalXml.Contains("extensionElements", StringComparison.Ordinal);
		var hasMultiInstance = originalXml.Contains("multiInstanceLoopCharacteristics", StringComparison.Ordinal) || originalXml.Contains("standardLoopCharacteristics", StringComparison.Ordinal);
		var hasDiagramInterchange = originalXml.Contains("BPMNDiagram", StringComparison.Ordinal);
		return (!hasExtensions || raw.RawExtensionElements is not null) && (!hasMultiInstance || raw.RawMultiInstance is not null) && (!hasDiagramInterchange || raw.RawDiRoot is not null);
	}

	private static string ParseLocalName(string qname)
	{
		var index = qname.LastIndexOf(':');
		if (index >= 0)
		{
			return qname[(index + 1)..];
		}

		return qname.StartsWith('{') ? qname[(qname.IndexOf('}', StringComparison.Ordinal) + 1)..] : qname;
	}
	private static string? ParseNamespace(string qname)
	{
		if (!qname.StartsWith('{'))
		{
			return null;
		}

		var close = qname.IndexOf('}', StringComparison.Ordinal);
		return close > 1 ? qname[1..close] : null;
	}

	private static bool TryParseExtensionKey(string key, out string nsUri, out string localName, out string attrName, out string attrNsUri)
	{
		nsUri = localName = attrName = attrNsUri = string.Empty;
		if (string.IsNullOrWhiteSpace(key))
		{
			return false;
		}

		string elementQName;
		var closeBrace = key.IndexOf('}', StringComparison.Ordinal);
		var dot = key.IndexOf('.', closeBrace >= 0 ? closeBrace + 1 : 0);
		if (dot > 0)
		{
			elementQName = key[..dot];
			var attributeQName = key[(dot + 1)..];
			if (!TryParseQualifiedName(elementQName, out nsUri, out localName))
			{
				return false;
			}

			if (attributeQName == "__present")
			{
				attrName = attributeQName;
				return true;
			}

			return TryParseQualifiedName(attributeQName, out attrNsUri, out attrName);
		}

		if (closeBrace > 0)
		{
			var separator = key.IndexOf(':', closeBrace + 1);
			if (separator <= closeBrace + 1)
			{
				return false;
			}

			elementQName = key[..separator];
			attrName = key[(separator + 1)..];
			if (!TryParseQualifiedName(elementQName, out nsUri, out localName))
			{
				return false;
			}

			return IsValidXmlLocalName(attrName);
		}

		if (!TryParseQualifiedName(key, out nsUri, out localName))
		{
			return false;
		}

		attrName = "__present";
		return true;
	}

	private static bool TryParseQualifiedName(string qname, out string nsUri, out string localName)
	{
		nsUri = localName = string.Empty;
		if (qname.StartsWith('{'))
		{
			var close = qname.IndexOf('}', StringComparison.Ordinal);
			if (close <= 1)
			{
				return false;
			}

			nsUri = qname[1..close];
			localName = qname[(close + 1)..];
		}
		else
		{
			var colon = qname.IndexOf(':', StringComparison.Ordinal);
			if (colon >= 0)
			{
				if (colon == 0 || colon == qname.Length - 1)
				{
					return false;
				}

				var prefix = qname[..colon];
				localName = qname[(colon + 1)..];
				nsUri = NamespaceByPrefix.TryGetValue(prefix, out var mapped) ? mapped : string.Empty;
			}
			else
			{
				localName = qname;
			}
		}

		return IsValidXmlLocalName(localName);
	}

	private static bool IsValidXmlLocalName(string name)
	{
		try
		{
			System.Xml.XmlConvert.VerifyNCName(name);
			return true;
		}
		catch (System.Xml.XmlException)
		{
			return false;
		}
	}

	private static readonly Dictionary<string, string> NamespaceByPrefix = new(StringComparer.OrdinalIgnoreCase)
	{
		["camunda"] = BpmnXmlNamespaces.Camunda,
		["zeebe"] = BpmnXmlNamespaces.Zeebe,
		["vertex"] = "https://vertexbpmn.io/schema/bpmn/1.0",
		["xsi"] = "http://www.w3.org/2001/XMLSchema-instance",
		["w4graph"] = BpmnXmlNamespaces.W4Graph
	};
	private static XElement SerializeLoop(LoopCharacteristics loop)
	{
		if (loop is StandardLoopCharacteristics standard)
		{
			var element = new XElement(Bpmn + "standardLoopCharacteristics", new XAttribute("testBefore", standard.TestBefore));
			if (standard.LoopMaximum.HasValue)
			{
				element.Add(new XAttribute("loopMaximum", standard.LoopMaximum.Value));
			}

			if (standard.LoopCondition is not null)
			{
				element.Add(new XElement(Bpmn + "loopCondition", standard.LoopCondition));
			}

			return element;
		}

		var multi = (MultiInstanceLoopCharacteristics)loop;
		var result = new XElement(Bpmn + "multiInstanceLoopCharacteristics", new XAttribute("isSequential", multi.IsSequential));
		if (multi.LoopCardinality.HasValue)
		{
			result.Add(new XElement(Bpmn + "loopCardinality", multi.LoopCardinality.Value));
		}

		if (multi.Collection is not null)
		{
			result.Add(new XAttribute(XName.Get("collection", BpmnXmlNamespaces.Camunda), multi.Collection));
		}

		if (multi.ElementVariable is not null)
		{
			result.Add(new XAttribute(XName.Get("elementVariable", BpmnXmlNamespaces.Camunda), multi.ElementVariable));
		}

		if (multi.InputElement is not null)
		{
			result.Add(new XElement(XName.Get("inputElement", BpmnXmlNamespaces.Zeebe), multi.InputElement));
		}

		if (multi.OutputElement is not null)
		{
			result.Add(new XElement(XName.Get("outputElement", BpmnXmlNamespaces.Zeebe), multi.OutputElement));
		}

		if (multi.CompletionCondition is not null)
		{
			result.Add(new XElement(Bpmn + "completionCondition", multi.CompletionCondition));
		}

		return result;
	}

	private static string? ParseElementNamespace(string key)
	{
		if (key.StartsWith('{'))
		{
			var brace = key.IndexOf('}', StringComparison.Ordinal);
			if (brace > 1)
			{
				return key[1..brace];
			}
		}

		var separator = key.IndexOfAny([':', '.']);
		if (separator > 0 && NamespaceByPrefix.TryGetValue(key[..separator], out var uri))
		{
			return uri;
		}

		return null;
	}

	private static XElement SerializeEventDefinition(EventDefinition def) => def switch
	{
		TimerEventDefinition t => new(Bpmn + "timerEventDefinition", t.TimeDate != null ? new XElement(Bpmn + "timeDate", t.TimeDate) : null, t.TimeDuration != null ? new XElement(Bpmn + "timeDuration", t.TimeDuration) : null, t.TimeCycle != null ? new XElement(Bpmn + "timeCycle", t.TimeCycle) : null),
		MessageEventDefinition m => new(Bpmn + "messageEventDefinition", new XAttribute("messageRef", m.MessageRef)),
		SignalEventDefinition s => new(Bpmn + "signalEventDefinition", new XAttribute("signalRef", s.SignalRef)),
		ErrorEventDefinition e => new(Bpmn + "errorEventDefinition", new XAttribute("errorRef", e.ErrorRef)),
		ConditionalEventDefinition c => new(Bpmn + "conditionalEventDefinition", new XElement(Bpmn + "conditionExpression", new XCData(c.Condition ?? string.Empty))),
		TerminateEventDefinition => new(Bpmn + "terminateEventDefinition"),
		CancelEventDefinition => new(Bpmn + "cancelEventDefinition"),
		CompensationEventDefinition cmp => new(Bpmn + "compensateEventDefinition", cmp.ActivityRef != null ? new XAttribute("activityRef", cmp.ActivityRef) : null),
		EscalationEventDefinition esc => new(Bpmn + "escalationEventDefinition", new XAttribute("escalationRef", esc.EscalationRef)),
		LinkEventDefinition link => new(Bpmn + "linkEventDefinition", new XAttribute("name", link.Name)),
		_ => new(Bpmn + "unsupportedEventDefinition")
	};
}
