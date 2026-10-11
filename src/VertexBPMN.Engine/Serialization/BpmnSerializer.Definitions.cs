using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Serialization;

public partial class BpmnSerializer
{
	private (Dictionary<string, List<string>>? Incoming, Dictionary<string, List<string>>? Outgoing) BuildFallbackFlowReferences(BpmnModel model, bool strict, bool strictRequested)
	{
		Dictionary<string, List<string>>? fallbackIncoming = null;
		Dictionary<string, List<string>>? fallbackOutgoing = null;
		if (strict || (strictRequested && PreserveGeneratedIfMissing))
		{
			fallbackIncoming = new(StringComparer.Ordinal);
			fallbackOutgoing = new(StringComparer.Ordinal);
			foreach (var f in model.SequenceFlows ?? [])
			{
				if (!fallbackOutgoing.TryGetValue(f.SourceRef, out var lo))
				{
					lo = [];
					fallbackOutgoing[f.SourceRef] = lo;
				}

				lo.Add(f.Id);
				if (!fallbackIncoming.TryGetValue(f.TargetRef, out var li))
				{
					li = [];
					fallbackIncoming[f.TargetRef] = li;
				}

				li.Add(f.Id);
			}
		}
		return (fallbackIncoming, fallbackOutgoing);
	}

	private static void CollectExtensionNamespaces(Dictionary<string, string>? ext, HashSet<string> vendorNs)
	{
		if (ext == null)
		{
			return;
		}

		foreach (var k in ext.Keys)
		{
			var elemNs = ParseElementNamespace(k);
			if (elemNs != null && elemNs != Bpmn.NamespaceName)
			{
				vendorNs.Add(elemNs);
			}
		}
	}


	private static HashSet<string> CollectVendorNamespaces(BpmnModel model, bool strict)
	{
		var vendorNs = new HashSet<string>(StringComparer.Ordinal);
		if (!strict)
		{

			foreach (var e in model.Events ?? [])
			{
				CollectExtensionNamespaces(e.ExtensionAttributes, vendorNs);
			}

			foreach (var g in model.Gateways ?? [])
			{
				CollectExtensionNamespaces(g.ExtensionAttributes, vendorNs);
			}

			foreach (var s in model.Subprocesses ?? [])
			{
				CollectExtensionNamespaces(s.ExtensionAttributes, vendorNs);
			}

			foreach (var t in model.Tasks ?? [])
			{
				CollectExtensionNamespaces(t.Attributes, vendorNs);
			}

			foreach (var f in model.SequenceFlows ?? [])
			{
				CollectExtensionNamespaces(f.ExtensionAttributes, vendorNs);
				if (f.Priority.HasValue)
				{
					vendorNs.Add(BpmnXmlNamespaces.Vertex);
				}
			}

			foreach (var sp in model.Subprocesses ?? [])
			{
				if (sp.Loop is MultiInstanceLoopCharacteristics mi)
				{
					if (!string.IsNullOrWhiteSpace(mi.Collection) || !string.IsNullOrWhiteSpace(mi.ElementVariable))
					{
						vendorNs.Add(BpmnXmlNamespaces.Camunda);
					}

					if (!string.IsNullOrWhiteSpace(mi.InputElement) || !string.IsNullOrWhiteSpace(mi.OutputElement))
					{
						vendorNs.Add(BpmnXmlNamespaces.Zeebe);
					}
				}
			}
		}
		return vendorNs;
	}

	private static void AppendGlobalMessages(BpmnModel model, XElement definitions)
	{
		if (model.Messages is { Count: > 0 })
		{
			foreach (var m in model.Messages)
			{
				if (string.IsNullOrEmpty(m.Id))
				{
					continue;
				}

				var msgEl = new XElement(Bpmn + "message", new XAttribute("id", m.Id));
				if (!string.IsNullOrWhiteSpace(m.Name))
				{
					msgEl.SetAttributeValue("name", m.Name);
				}

				definitions.Add(msgEl);
			}
		}
	}

	private static void AppendGlobalSignals(BpmnModel model, XElement definitions)
	{
		if (model.Signals is { Count: > 0 })
		{
			foreach (var s in model.Signals)
			{
				if (string.IsNullOrEmpty(s.Id))
				{
					continue;
				}

				var sigEl = new XElement(Bpmn + "signal", new XAttribute("id", s.Id));
				if (!string.IsNullOrWhiteSpace(s.Name))
				{
					sigEl.SetAttributeValue("name", s.Name);
				}

				definitions.Add(sigEl);
			}
		}
	}

	private static void AppendGlobalErrors(BpmnModel model, XElement definitions)
	{
		if (model.Errors is { Count: > 0 })
		{
			foreach (var e in model.Errors)
			{
				if (string.IsNullOrEmpty(e.Id))
				{
					continue;
				}

				var errEl = new XElement(Bpmn + "error", new XAttribute("id", e.Id));
				if (!string.IsNullOrWhiteSpace(e.Name))
				{
					errEl.SetAttributeValue("name", e.Name);
				}

				if (!string.IsNullOrWhiteSpace(e.ErrorCode))
				{
					errEl.SetAttributeValue("errorCode", e.ErrorCode);
				}

				definitions.Add(errEl);
			}
		}
	}

	private static void AppendGlobalEscalations(BpmnModel model, XElement definitions)
	{
		if (model.Escalations is { Count: > 0 })
		{
			foreach (var esc in model.Escalations)
			{
				if (string.IsNullOrEmpty(esc.Id))
				{
					continue;
				}

				var escEl = new XElement(Bpmn + "escalation", new XAttribute("id", esc.Id));
				if (!string.IsNullOrWhiteSpace(esc.Name))
				{
					escEl.SetAttributeValue("name", esc.Name);
				}

				if (!string.IsNullOrWhiteSpace(esc.EscalationCode))
				{
					escEl.SetAttributeValue("escalationCode", esc.EscalationCode);
				}

				definitions.Add(escEl);
			}
		}
	}

	private static void AppendDefinitionsGlobals(BpmnModel model, bool strict, BpmnRawMetadata? raw, XElement definitions)
	{
		if (strict && raw?.RawGlobalElements is { Count: > 0 })
		{
			foreach (var ge in raw.RawGlobalElements)
			{
				definitions.Add(new XElement(ge));
			}
			return;
		}
		AppendGlobalMessages(model, definitions);
		AppendGlobalSignals(model, definitions);
		if (!strict)
		{
			AppendGlobalErrors(model, definitions);
			AppendGlobalEscalations(model, definitions);
		}
	}
}

