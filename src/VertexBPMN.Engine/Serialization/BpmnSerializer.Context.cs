using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Serialization;

public partial class BpmnSerializer
{
	private sealed class SerializationContext(
		BpmnSerializer serializer,
		BpmnModel model,
		bool strict,
		BpmnRawMetadata? raw,
		XElement proc,
		Dictionary<string, List<string>>? fallbackIncoming,
		Dictionary<string, List<string>>? fallbackOutgoing)
	{
		private readonly List<string>? orderedElements = strict && raw?.ElementsMetadata != null
			? raw.ElementsMetadata.OrderBy(k => k.Value.OrderIndex).Select(k => k.Key).ToList() : null;
		private readonly Dictionary<string, XElement> elementLookup = new(StringComparer.Ordinal);

		internal void AppendFlowNodes()
		{
			AppendSubprocesses();
			AppendEvents();
			AppendGateways();
			AppendTasks();
			AppendSequenceFlows();
			AppendOrderedElements();
		}

		private void AddRawDocumentation(string id, XElement node)
		{
			if (!strict)
			{
				return;
			}

			if (raw?.RawDocumentation != null && raw.RawDocumentation.TryGetValue(id, out var docs))
			{
				foreach (var d in docs)
				{
					node.Add(new XElement(d));
				}
			}
		}

		private static void AddExtensionsNormalized(XElement parent, Dictionary<string, string>? ext)
		{
			if (ext == null || ext.Count == 0)
			{
				return;
			}

			var extRoot = new XElement(Bpmn + "extensionElements");
			var elementMap = new Dictionary<string, XElement>(StringComparer.Ordinal);
			const string vertexUri = VertexBPMN.Engine.Parsing.VertexBpmnExtensions.NamespaceUri;
			var mappings = ext.Where(item => item.Key.StartsWith("vertex:ioMapping.input.", StringComparison.Ordinal) || item.Key.StartsWith("vertex:ioMapping.output.", StringComparison.Ordinal)).ToArray();
			foreach (var kv in ext)
			{
				if (!TryParseExtensionKey(kv.Key, out var nsUri, out var localName, out var attrName, out var attrNsUri))
				{
					continue;
				}

				// Nested mappings cannot be flattened into attributes on ioMapping or
				// emitted again as sibling input/output elements from raw projections.
				if (mappings.Length > 0 && nsUri == vertexUri && localName is "ioMapping" or "input" or "output")
				{
					continue;
				}

				var key = nsUri + "|" + localName;
				if (!elementMap.TryGetValue(key, out var el))
				{
					XNamespace nsx = nsUri;
					el = new XElement(nsx + localName);
					elementMap[key] = el;
					extRoot.Add(el);
				}

				if (attrName == "__present")
				{
					continue;
				}

				if (attrName == "__text")
				{
					el.Value = kv.Value;
					continue;
				}

				el.SetAttributeValue(string.IsNullOrEmpty(attrNsUri) ? XName.Get(attrName) : XName.Get(attrName, attrNsUri), kv.Value);
			}

			AppendVertexIoMappings(extRoot, mappings);
			if (extRoot.HasElements)
			{
				parent.Add(extRoot);
			}
		}

		private static void AppendVertexIoMappings(XElement extRoot, KeyValuePair<string, string>[] mappings)
		{
			const string vertexUri = VertexBPMN.Engine.Parsing.VertexBpmnExtensions.NamespaceUri;
			if (mappings.Length > 0)
			{
				XNamespace vertex = vertexUri;
				var mapping = new XElement(vertex + "ioMapping");
				foreach (var item in mappings.OrderBy(item => item.Key, StringComparer.Ordinal))
				{
					var input = item.Key.StartsWith("vertex:ioMapping.input.", StringComparison.Ordinal);
					var prefix = input ? "vertex:ioMapping.input." : "vertex:ioMapping.output.";
					mapping.Add(new XElement(vertex + (input ? "input" : "output"), new XAttribute("name", item.Key[prefix.Length..]), new XAttribute(input ? "expression" : "target", item.Value)));
				}

				extRoot.Add(mapping);
			}

		}

		private void AttachRawExtensions(string id, XElement target)
		{
			if (!strict || raw?.RawExtensionElements == null)
			{
				return;
			}

			if (raw.RawExtensionElements.TryGetValue(id, out var rawExt))
			{
				target.Add(new XElement(rawExt));
			}

		}

		private void AppendInOutIfStrict(string id, XElement node)
		{
			if (!strict)
			{
				return;
			}

			IReadOnlyList<string>? incList = null;
			IReadOnlyList<string>? outList = null;
			if (raw?.Incoming != null && raw.Incoming.TryGetValue(id, out var ri))
			{
				incList = ri;
			}
			else if (serializer.PreserveGeneratedIfMissing && fallbackIncoming != null && fallbackIncoming.TryGetValue(id, out var fi))
			{
				incList = fi;
			}

			if (raw?.Outgoing != null && raw.Outgoing.TryGetValue(id, out var ro))
			{
				outList = ro;
			}
			else if (serializer.PreserveGeneratedIfMissing && fallbackOutgoing != null && fallbackOutgoing.TryGetValue(id, out var fo))
			{
				outList = fo;
			}

			if (incList?.Count > 0)
			{
				foreach (var iid in incList)
				{
					node.Add(new XElement(Bpmn + "incoming", iid));
				}
			}

			if (outList?.Count > 0)
			{
				foreach (var oid in outList)
				{
					node.Add(new XElement(Bpmn + "outgoing", oid));
				}
			}
		}

		private void ApplyOriginalAttributes(string id, XElement el)
		{
			if (!strict || raw?.FlowNodeAttributes == null)
			{
				return;
			}

			var isPartialDirty = raw.PartiallyDirtyElements?.Contains(id) == true;
			if (!raw.FlowNodeAttributes.TryGetValue(id, out var attrs))
			{
				return;
			}

			foreach (var kv in attrs)
			{
				var local = ParseLocalName(kv.Key);
				if (local == "id")
				{
					continue;
				}

				if (local == "name" && isPartialDirty)
				{
					continue;
				}

				if (local == "name" && string.IsNullOrEmpty(kv.Value))
				{
					continue;
				}

				if (string.IsNullOrEmpty(local) || local.Contains('/', StringComparison.Ordinal) || local.Any(ch => char.IsWhiteSpace(ch)))
				{
					continue;
				}

				var nsUri = ParseNamespace(kv.Key);
				var xname = nsUri == null ? local : XName.Get(local, nsUri);
				if (el.Attribute(xname) == null)
				{
					el.Add(new XAttribute(xname, kv.Value));
				}
			}

			if (isPartialDirty)
			{
				var task = (model.Tasks ?? []).FirstOrDefault(t => t.Id == id);
				if (task != null && !string.IsNullOrEmpty(task.Name))
				{
					el.SetAttributeValue("name", task.Name);
				}
			}
		}

		private XElement BuildSubProcess(BpmnSubprocess sp)
		{
			var spEl = new XElement(Bpmn + "subProcess", new XAttribute("id", sp.Id));
			if (!strict && sp.Loop is MultiInstanceLoopCharacteristics mi)
			{
				AppendSubprocessMultiInstance(spEl, mi);
			}
			else if (strict && raw?.RawMultiInstance != null && raw.RawMultiInstance.TryGetValue(sp.Id, out var rawMi))
			{
				spEl.Add(new XElement(rawMi));
			}

			if (strict)
			{
				AttachRawExtensions(sp.Id, spEl);
			}
			else
			{
				AddExtensionsNormalized(spEl, sp.ExtensionAttributes);
			}

			ApplyOriginalAttributes(sp.Id, spEl);
			AddRawDocumentation(sp.Id, spEl);
			AppendInOutIfStrict(sp.Id, spEl);
			return spEl;
		}

		private void Register(string id, XElement el)
		{
			if (orderedElements != null && !string.IsNullOrEmpty(id))
			{
				elementLookup[id] = el;
			}
			else
			{
				proc.Add(el);
			}
		}

		internal void AppendSubprocesses()
		{
			foreach (var sp in model.Subprocesses ?? [])
			{
				Register(sp.Id, BuildSubProcess(sp));
			}
		}

		internal void AppendEvents()
		{
			foreach (var evt in model.Events ?? [])
			{
				var evtEl = new XElement(Bpmn + evt.Type, new XAttribute("id", evt.Id));
				if (strict && raw?.RawEventDefinitions != null && raw.RawEventDefinitions.TryGetValue(evt.Id, out var rawDefs))
				{
					foreach (var rd in rawDefs)
					{
						evtEl.Add(new XElement(rd));
					}
				}
				else
				{
					foreach (var def in evt.Definitions ?? [])
					{
						evtEl.Add(SerializeEventDefinition(def));
					}
				}

				if (strict)
				{
					AttachRawExtensions(evt.Id, evtEl);
				}
				else
				{
					AddExtensionsNormalized(evtEl, evt.ExtensionAttributes);
				}

				ApplyOriginalAttributes(evt.Id, evtEl);
				AddRawDocumentation(evt.Id, evtEl);
				AppendInOutIfStrict(evt.Id, evtEl);
				Register(evt.Id, evtEl);
			}
		}

		internal void AppendGateways()
		{
			foreach (var gw in model.Gateways ?? [])
			{
				var gwEl = new XElement(Bpmn + gw.Type, new XAttribute("id", gw.Id));
				if (!string.IsNullOrWhiteSpace(gw.DefaultFlowId))
				{
					gwEl.Add(new XAttribute("default", gw.DefaultFlowId));
				}

				if (strict)
				{
					AttachRawExtensions(gw.Id, gwEl);
				}
				else
				{
					AddExtensionsNormalized(gwEl, gw.ExtensionAttributes);
				}

				ApplyOriginalAttributes(gw.Id, gwEl);
				AddRawDocumentation(gw.Id, gwEl);
				AppendInOutIfStrict(gw.Id, gwEl);
				Register(gw.Id, gwEl);
			}
		}

		internal void AppendTasks()
		{
			foreach (var task in model.Tasks ?? [])
			{
				var taskEl = new XElement(Bpmn + task.Type, new XAttribute("id", task.Id));
				if (!strict && !string.IsNullOrWhiteSpace(task.Name))
				{
					taskEl.SetAttributeValue("name", task.Name);
				}

				if (strict && raw?.RawMultiInstance != null && raw.RawMultiInstance.TryGetValue(task.Id, out var rawMi))
				{
					taskEl.Add(new XElement(rawMi));
				}
				else if (task.Loop is { } loop)
				{
					taskEl.Add(SerializeLoop(loop));
				}

				if (strict)
				{
					AttachRawExtensions(task.Id, taskEl);
				}
				else
				{
					AddExtensionsNormalized(taskEl, task.Attributes);
				}

				ApplyOriginalAttributes(task.Id, taskEl);
				AddRawDocumentation(task.Id, taskEl);
				AppendInOutIfStrict(task.Id, taskEl);
				Register(task.Id, taskEl);
			}
		}

		internal void AppendSequenceFlows()
		{
			foreach (var f in model.SequenceFlows ?? [])
			{
				var fEl = new XElement(Bpmn + "sequenceFlow", new XAttribute("id", f.Id), new XAttribute("sourceRef", f.SourceRef), new XAttribute("targetRef", f.TargetRef));
				if (f.Priority.HasValue)
				{
					if (strict && raw?.PriorityAttributeNamespace != null && raw.PriorityAttributeNamespace.TryGetValue(f.Id, out var pns))
					{
						if (string.IsNullOrEmpty(pns))
						{
							fEl.SetAttributeValue("priority", f.Priority.Value);
						}
						else
						{
							fEl.SetAttributeValue(XName.Get("priority", pns), f.Priority.Value);
						}
					}
					else
					{
						fEl.SetAttributeValue(XName.Get("priority", BpmnXmlNamespaces.Vertex), f.Priority.Value);
					}
				}

				AppendSequenceFlowCondition(f, fEl);
				if (strict)
				{
					AttachRawExtensions(f.Id, fEl);
				}
				else
				{
					AddExtensionsNormalized(fEl, f.ExtensionAttributes);
				}

				ApplyOriginalAttributes(f.Id, fEl);
				AddRawDocumentation(f.Id, fEl);
				Register(f.Id, fEl);
			}
		}

		private void AppendSequenceFlowCondition(BpmnSequenceFlow f, XElement fEl)
		{
			if (strict && raw?.SequenceFlowConditions != null && raw.SequenceFlowConditions.TryGetValue(f.Id, out var rcond))
			{
				if (!string.IsNullOrEmpty(rcond.Raw))
				{
					var condEl = new XElement(Bpmn + "conditionExpression");
					if (!string.IsNullOrWhiteSpace(f.ConditionExpressionLanguage))
					{
						condEl.SetAttributeValue("language", f.ConditionExpressionLanguage);
					}

					if (rcond.WasCData)
					{
						condEl.Add(new XCData(rcond.Raw));
					}
					else
					{
						condEl.Value = rcond.Raw;
					}

					fEl.Add(condEl);
				}
			}
			else if (!string.IsNullOrWhiteSpace(f.ConditionExpression))
			{
				var condEl = new XElement(Bpmn + "conditionExpression", new XCData(f.ConditionExpression));
				if (!string.IsNullOrWhiteSpace(f.ConditionExpressionLanguage))
				{
					condEl.SetAttributeValue("language", f.ConditionExpressionLanguage);
				}

				fEl.Add(condEl);
			}

		}

		internal void AppendOrderedElements()
		{
			if (orderedElements != null)
			{
				foreach (var id in orderedElements)
				{
					if (elementLookup.TryGetValue(id, out var el))
					{
						proc.Add(el);
					}
				}
			}
		}
		private static void AppendSubprocessMultiInstance(XElement spEl, MultiInstanceLoopCharacteristics mi)
		{
			var miEl = new XElement(Bpmn + "multiInstanceLoopCharacteristics");
			if (mi.IsSequential)
			{
				miEl.Add(new XAttribute("isSequential", "true"));
			}

			if (mi.LoopCardinality.HasValue)
			{
				miEl.Add(new XElement(Bpmn + "loopCardinality", mi.LoopCardinality.Value));
			}

			if (!string.IsNullOrWhiteSpace(mi.Collection))
			{
				miEl.Add(new XAttribute(XName.Get("collection", BpmnXmlNamespaces.Camunda), mi.Collection));
			}

			if (!string.IsNullOrWhiteSpace(mi.ElementVariable))
			{
				miEl.Add(new XAttribute(XName.Get("elementVariable", BpmnXmlNamespaces.Camunda), mi.ElementVariable));
			}

			if (!string.IsNullOrWhiteSpace(mi.InputElement))
			{
				miEl.Add(new XElement(XName.Get("inputElement", BpmnXmlNamespaces.Zeebe), mi.InputElement));
			}

			if (!string.IsNullOrWhiteSpace(mi.OutputElement))
			{
				miEl.Add(new XElement(XName.Get("outputElement", BpmnXmlNamespaces.Zeebe), mi.OutputElement));
			}

			if (!string.IsNullOrWhiteSpace(mi.CompletionCondition))
			{
				miEl.Add(new XElement(Bpmn + "completionCondition", mi.CompletionCondition));
			}

			spEl.Add(miEl);
		}
	}
}
